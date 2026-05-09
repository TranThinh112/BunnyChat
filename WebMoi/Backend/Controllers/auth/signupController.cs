using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using WebMoi.Data;
using WebMoi.DTOs;
using WebMoi.Models;
using WebMoi.Models.Entities;
using BCrypt.Net;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace WebMoi.Controllers


{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        
        private readonly IMongoCollection<User> _usersCollection;

        public AuthController(MongoDbService mongoDbService)
        {
            _usersCollection = mongoDbService.Database.GetCollection<User>("users");
        }
       
        [HttpPost("signup")]
        public async Task<ActionResult> Create(Signup request) 
        {
            try
            {
                //kiểm tra username
                var  duplicateUser = await _usersCollection.Find(u => u.Username == request.UserName || u.Email == request.Email).FirstOrDefaultAsync();

                if (duplicateUser != null) { 
                    
                    return StatusCode(409,ApiResponse.Fail(
                      message: "Username hoặc Email đã tồn tại" 
                    ));
                }

                //data gửi lên server
                var user = new User
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Username = request.UserName,
                    Email = request.Email,
                    HashPassword = BCrypt.Net.BCrypt.HashPassword(request.PassWord, 10),
                    CreatedAt = DateTime.UtcNow,
                };

                await _usersCollection.InsertOneAsync(user);

                //trả về Response
                return Ok(ApiResponse.Success(
                    message: "Tạo tài khoản thành công",
                    new
                    {
                        id = user.Id,
                        username = user.Username,
                        displayname = user.DisplayName
                    }
                ));

            }
            catch
            {
               return StatusCode(500, ApiResponse.Fail(
                    message: "Lỗi khi Signup"
               ));
                
            }
        }
    }
}
