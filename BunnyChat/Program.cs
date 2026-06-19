using Microsoft.AspNetCore.Identity;
using BunnyChat.Service;
using BunnyChat.Service;


//đăng ký authen
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;

//config cho FrontEnd
using Microsoft.Extensions.FileProviders;


var builder = WebApplication.CreateBuilder(args);


//Swwagger
builder.Services.AddControllers();

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
var port = Environment.GetEnvironmentVariable("PORT") ?? "5281";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var mongoService =
        scope.ServiceProvider
            .GetRequiredService<MongoDbService>();

    await mongoService.CreateIndexesAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

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

app.Lifetime.ApplicationStarted.Register(() =>
{
    foreach (var url in app.Urls)
    {
        Console.WriteLine($"🌐 Server dang chay tai: {url}");
    }
});



app.Run();
