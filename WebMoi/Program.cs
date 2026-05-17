using Microsoft.AspNetCore.Identity;
using WebMoi.Data;
using WebMoi.Service;

//đăng ký authen
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;

//hàm trả về lỗi trong middleware
static async Task MiddlewareError(HttpContext context)
{
    context.Response.StatusCode = 500;
    context.Response.ContentType = "application/json";

    await context.Response.WriteAsync(
        JsonSerializer.Serialize(
            ApiResponse.Fail("Lỗi xác minh token")
        )
    );
}


var builder = WebApplication.CreateBuilder(args);

//Swwagger
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// Add services to the container.
builder.Services.AddControllersWithViews();
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

            // token có hết hạn chưa
            ValidateLifetime = true,

            // quy định mức sai lệch thời gian được phép khi server validate JWT.
            ClockSkew = TimeSpan.Zero
        };
        //các trạng thái lỗi của middleware và respon
        options.Events = new JwtBearerEvents
        {
            
            // token thiếu 
            OnChallenge = async context =>
            {
                try
                {
                    context.HandleResponse();

                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";

                    await context.Response.WriteAsync(
                        JsonSerializer.Serialize(
                            ApiResponse.Fail("Không tìm thấy access token")
                        )
                    );
                }
                catch
                {
                    await MiddlewareError(context.HttpContext);
                }
            },

            //token sai or hết hạn
            OnAuthenticationFailed = async context =>
            {
                try
                {
                    context.NoResult();

                    context.Response.StatusCode = 403;
                    context.Response.ContentType = "application/json";

                    await context.Response.WriteAsync(
                        JsonSerializer.Serialize(
                            ApiResponse.Fail("Access token hết hạn hoặc không đúng ")
                        )
                    );
                }
                catch
                {
                    await MiddlewareError(context.HttpContext);
                }
            },
        };
    });
    

builder.Services.AddAuthorization();
    

var app = builder.Build();

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
app.UseStaticFiles();

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
    pattern: "{controller=Home}/{action=Index}/{id?}"
    );

// app.MapControllers();

app.Lifetime.ApplicationStarted.Register(() =>
{
    foreach (var url in app.Urls)
    {
        Console.WriteLine($"🌐 Server dang chay tai: {url}");
    }
});

app.MapControllers();


app.Run();
