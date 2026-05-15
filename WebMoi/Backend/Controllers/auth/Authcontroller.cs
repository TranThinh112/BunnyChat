using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using WebMoi.Data;
using WebMoi.DTOs;
using WebMoi.Models;
using WebMoi.Models.Entities;
using WebMoi.Service;
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
        private readonly ITokenService _tokenService;
        private readonly IConfiguration _configuration;


        public AuthController(MongoDbService mongoDbService, ITokenService tokenService, IConfiguration configuration)
        {
            _usersCollection = mongoDbService.Database.GetCollection<User>("users");
            _sessionCollection = mongoDbService.Database.GetCollection<Session>("sessions");
            _tokenService = tokenService;
            _configuration = configuration;
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
                    Nickname = request.NickName, //Nickname có thể có hoawjkc ko 
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
               //Lấy input
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

                //kiểm tra password có đúng hjay ko 
                var passwordCorrect = BCrypt.Net.BCrypt.Verify(request.PassWord, user.HashPassword);
                if(!passwordCorrect)
                {
                    return Unauthorized(ApiResponse.Fail(
                        message: "Username hoặc Password không chính xác"
                    ));
                }

                //nếu khớp, tạo accesToken với JWT
                var accessToken = _tokenService.CreateAccessToken(user);

                //tạo refreshToken
                var refreshToken = _tokenService.CreateRefreshToken();

                //thời gian hết hạn của Refresh Token
                var refreshExpiry = DateTime.UtcNow.AddDays(
                             Convert.ToDouble(_configuration["JwtSettings:RefreshTokenExpirationDays"])
                            );         

                //import vao collectiuon sessiuon, nếu đã có trong collection, update RefreshToken
                await _sessionCollection.ReplaceOneAsync(
                    s => s.Username == user.Username,
                    new Session
                    {
                        Username = user.Username,
                        RefreshToken = refreshToken,
                        // RefreshToken = BCrypt.Net.BCrypt.HashPassword(refreshToken, 10),
                        ExpiresAt = refreshExpiry
                    },
                    new ReplaceOptions { IsUpsert = true }
                );
            
                //Gắn refreshToken vào cookie
                Response.Cookies.Append(
                    "refreshToken",
                    refreshToken,
                    new CookieOptions
                    {
                        //Chống XSS
                        HttpOnly = true,

                        //Gwuir cookie qua HTTPS, ko gửi qua HTTP
                        Secure = true,

                        // Chống CSRF
                        SameSite = SameSiteMode.Strict,

                        //  hết hạn sau 7 ngày.
                        Expires = refreshExpiry 
                    }
                );

                //trả về accesToken
                return Ok(ApiResponse.Success(
                    message: $"{request.UserName} đăng nhập thành công",
                    new
                    {   
                        accessToken,
                    }
                ));
            }
            // trả về lỗi
            catch(Exception ex)
            {
                Console.WriteLine(ex);

                return StatusCode(500, ApiResponse.Fail(
                    message: "Lỗi khi gọi Login"
                ));
            }
        }
    }
}
