using Microsoft.AspNetCore.Identity;
using BunnyChat.Service;


//đăng ký authen
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;
using BunnyChat.Backend.Hubs;

//config cho FrontEnd
using Microsoft.Extensions.FileProviders;


var builder = WebApplication.CreateBuilder(args);


//Swwagger
builder.Services.AddControllers();
builder.Services.AddSignalR();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// Add services to the container.
builder.Services
    .AddControllersWithViews()
    .AddRazorOptions(options =>
    {
        options.ViewLocationFormats.Clear();

        // Frontend/Views/{Controller}/{Action}.cshtml
        options.ViewLocationFormats.Add("/Frontend/Views/{1}/{0}.cshtml");

        // Frontend/Views/Shared/_Layout.cshtml
        options.ViewLocationFormats.Add("/Frontend/Views/Shared/{0}.cshtml");
    });


//add mongoDB
builder.Services.AddSingleton<MongoDbService>();

//Đămg ký AccessToken
builder.Services.AddScoped<ITokenService, TokenService>();


//đăng ký middleware authentication,    dạy server cách kiểm tra Bearer access token mà client gửi lên
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    {
        //luật kiểm tra token
        options.TokenValidationParameters = new TokenValidationParameters
        {

            // kiểm tra chữ ký token
            ValidateIssuerSigningKey = true,
            
            // secret key dùng verify token.
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    builder.Configuration["JwtSettings:SecurityKey"]!
                )
            ),
            // kiểm tra "ai phát hành token"    issuer = người phát hành token
            ValidateIssuer = false,
            
            // kiểm tra "token dành cho ai"     audience = người nhận token
            ValidateAudience = false,

            // token có hết hạn chưa
            ValidateLifetime = true,

            // quy định mức sai lệch thời gian được phép khi server validate JWT.
            ClockSkew = TimeSpan.Zero
        };
        //các trạng thái lỗi của middleware và respon
        options.Events = new JwtBearerEvents
        {
            // SignalR gửi access token qua query string khi mở websocket.
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrWhiteSpace(accessToken) &&
                    path.StartsWithSegments("/chatHub"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },

             //token sai or hết hạn
            OnAuthenticationFailed = async context =>
            {
                   Console.WriteLine(context.Exception.Message);
                context.NoResult();

                if(!context.Response.HasStarted)
                {
                    context.Response.StatusCode = 403;
                    context.Response.ContentType = "application/json";

                    await context.Response.WriteAsync(
                        JsonSerializer.Serialize(
                            ApiResponse.Fail("Access token hết hạn hoặc không đúng ")
                        )
                    );
                }
            },

            // token thiếu 
            OnChallenge = async context =>
            {
                    context.HandleResponse();
                    
                    if (context.Response.HasStarted)
                        return;

                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";

                    await context.Response.WriteAsync(
                        JsonSerializer.Serialize(
                            ApiResponse.Fail("Không tìm thấy access token")
                        )
                    );
            },
        };
    });
    

builder.Services.AddAuthorization();

// Railway sẽ truyền port qua biến môi trường PORT.
// Nếu chạy local không có PORT thì dùng 5281 để giữ thói quen chạy hiện tại.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    time = DateTime.UtcNow
}));

app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        try
        {
            using var scope = app.Services.CreateScope();

            var mongoService = scope.ServiceProvider
                .GetRequiredService<MongoDbService>();

            await mongoService.CreateIndexesAsync();
            Console.WriteLine("Tao index MongoDB thanh cong");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Khong the tao index MongoDB sau khi khoi dong: {ex.Message}");
        }
    });
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseSwagger();
app.UseSwaggerUI();

// Không ép redirect HTTPS để local chạy ổn trên http://localhost:5281
// và Railway tự xử lý HTTPS ở tầng proxy bên ngoài.

//static files
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "Frontend", "wwwroot")
    ),
    RequestPath = ""
});;

//phân tích URL và tìm route phù hợp
app.UseRouting();


app.UseAuthentication();


//chỉ định các API nào dùng middleware 
// app.UseWhen(
//     context => context.Request.Path.StartsWithSegments("/users/me"),
//     appBuilder =>
//     {
//         appBuilder.UseMiddleware<AuthMiddleware>();
//     });

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Page}/{action=Index}/{id?}"
    );

app.MapControllers();
//map với signalR
app.MapHub<ChatHub>("/chatHub");

app.Lifetime.ApplicationStarted.Register(() =>
{
    foreach (var url in app.Urls)
    {
        Console.WriteLine($"🌐 Server dang chay tai: {url}");
    }
});



app.Run();
