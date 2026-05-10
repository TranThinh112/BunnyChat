using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using WebMoi.Data;
using WebMoi.DTOs;
using WebMoi.Models;
using WebMoi.Models.Entities;
using  BCrypt.Net;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace WebMoi.Controllers

{
    [Route("auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        
        private readonly IMongoCollection<User> _usersCollection;
        private readonly IMongoCollection<Session> _sessionCollection;

        public AuthController(MongoDbService mongoDbService)
        {
            _usersCollection = mongoDbService.Database.GetCollection<User>("users");
            _sessionCollection = mongoDbService.Database.GetCollection<Session>("sessions");
            
        }
       
        [HttpPost("signup")]
        public async Task<ActionResult> CreateUser(SignUpRequest request) 
        {
            try
            {
                //kiểm tra có thiếu dữ liệu ko
                if(string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.PassWord) || string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName) || string.IsNullOrWhiteSpace(request.UserName))
                {
                    return BadRequest(ApiResponse.Fail(
                        message: "Không thể thiếu Eamil, Password, Firstname, Lastname và Username"
                    ));
                }
                Console.WriteLine(request.UserName);
                
                //kiểm tra username
                var  duplicateUser = await _usersCollection.Find(u => u.Username == request.UserName || u.Email == request.Email).FirstOrDefaultAsync();

                if (duplicateUser != null) { 
                    
                    return StatusCode(409,ApiResponse.Fail(
                      message: "Username hoặc Email đã tồn tại" 
                    ));
                }

                //mã hóa pâssword
                var HashedPassword = BCrypt.Net.BCrypt.HashPassword(request.PassWord, 10);

                //data gửi lên server
                var user = new User
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Username = request.UserName,
                    Email = request.Email,
                    HashPassword = HashedPassword,
                    CreatedAt = DateTime.UtcNow,
                };

                //Insert user mới vào bảng
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

            //trả về lỗi
            catch
            {
               return StatusCode(500, ApiResponse.Fail(
                    message: "Lỗi khi Signup"
               ));
            }
        }

        //Api Login
        [HttpPost("login")]
        public async Task<ActionResult> Login (LoginRequest request)
        {
            try
            {
                //kiểm tra có thiếu data hay ko 
                if( string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.PassWord))
                {
                    return BadRequest(ApiResponse.Fail(
                        message: "Thiếu Usernanme hoặc Password"
                    ));
                }
                
                //Kiểm tra username có tồn tại hay ko 
                var user = await _usersCollection.Find(u => u.Username == request.UserName).FirstOrDefaultAsync();

                if(user == null)
                {
                    return Unauthorized(ApiResponse.Fail(
                        message: "Username hoặc Password không chính xác"
                    ));
                }

                // Console.WriteLine(request.PassWord);
                // Console.WriteLine(user == null);
                // Console.WriteLine(user?.HashPassword);
                //kiểm tra password có đúng hjay ko 
                var passwordCorrect = BCrypt.Net.BCrypt.Verify(request.PassWord, user.HashPassword);
                //Sai password
                if(!passwordCorrect)
                {
                    return Unauthorized(ApiResponse.Fail(
                        message: "Username hoặc Password không chính xác"
                    ));
                }

                //data gui len collectiuon session
                var session = new Session
                {
                    Username = request.UserName,
                };

                //import vao collectiuon sessiuon
                await _sessionCollection.InsertOneAsync(session);

                //trả kết quả true
                return Ok(ApiResponse.Success(
                    message: $"{request.UserName} đăng nhập thành công"
                ));
            }
            // trả về lỗi
            catch
            {
                return StatusCode(500, ApiResponse.Fail(
                    message: "Lỗi khi gọi Login"
                ));
            }
        }
    }
}
