// using System.IdentityModel.Tokens.Jwt;
// using System.Security.Claims;
// using System.Text;
// using MongoDB.Driver;
// using BunnyChat.Data;
// using BunnyChat.DTOs;
// using BunnyChat.Models.Entities;

// using Microsoft.IdentityModel.Tokens;
// using DnsClient.Protocol;


// namespace BunnyChat.MiddleWares;
// public class AuthMiddleware
// {
//     // chuyen den middleware tiep theo
//     private readonly RequestDelegate _next;
//     private readonly IMongoCollection<User> _usersCollection;
//     private readonly IConfiguration _configuration;

//     public AuthMiddleware(
//         RequestDelegate next,
//         MongoDbService mongoDbService,
//         IConfiguration configuration    )
//     {
//         _next = next;

//         //trỏ đến collection sẽ thao tác
//        _usersCollection = mongoDbService.Database.GetCollection<User>("users");

//        //dùng để đọc config
//         _configuration = configuration;
//     }

//      public async Task InvokeAsync(HttpContext context)
//     {
//         Console.WriteLine("Middleware chạy");
//         try
//         {
//             //tìm user
//             var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

//             var user = await _usersCollection.Find(s => s.Id == userId).FirstOrDefaultAsync();
//              if(user != null)
//                 {
//                     Console.WriteLine($"Không tìm thấy user");

//                     context.Response.StatusCode = 404;
//                     await context.Response.WriteAsJsonAsync(ApiResponse.Fail(
//                         message: "Người dùng không tồn tại"
//                     ));
//                 }

//             //trả về user trong req
//             context.Items["User"] = user;
//         }

//         //trả về lỗi
//         catch(Exception ex)
//         {
//             Console.WriteLine($"Lỗi khi xác minh JWT trong AuthMiddleware: {ex.Message}");

//             context.Response.StatusCode = 500;
//             await context.Response.WriteAsJsonAsync(ApiResponse.Fail(
//                 message: "Lỗi hệ thống"
//             ));
//         }
//     }
// }
