using Microsoft.AspNetCore.Identity;
using WebMoi.Data;
using WebMoi.Service;


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

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
    );

app.Lifetime.ApplicationStarted.Register(() =>
{
    foreach (var url in app.Urls)
    {
        Console.WriteLine($"🌐 Server dang chay tai: {url}");
    }
});

app.MapControllers();


app.Run();
