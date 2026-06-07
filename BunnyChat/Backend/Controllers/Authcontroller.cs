using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MongoDB.Bson;
using BunnyChat.Service;
using BunnyChat.DTOs;
using BunnyChat.Models;
using BunnyChat.Helper;
using BunnyChat.Models;
using BunnyChat.Service;
using BCrypt.Net;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace BunnyChat.Controllers

{
    [Route("/api/auth")]
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
        public async Task<ActionResult> SignUp(SignUpDTORequest request)
        {
            try
            {
                //kiểm tra có thiếu dữ liệu ko
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.PassWord) || string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName) || string.IsNullOrWhiteSpace(request.UserName))
                {
                    return BadRequest(ApiResponse.Fail(
                        message: "Không thể thiếu Eamil, Password, Firstname, Lastname và Username"
                    ));
                }

                //kiểm tra username
                var duplicateUser = await _usersCollection.Find(u => u.Username == request.UserName || u.Email == request.Email).FirstOrDefaultAsync();

                if (duplicateUser != null)
                {

                    return StatusCode(409, ApiResponse.Fail(
                      message: "Username hoặc Email đã tồn tại"
                    ));
                }

                //mã hóa pâssword
                var HashedPassword = BCrypt.Net.BCrypt.HashPassword(request.PassWord, 10);

                var displayName = $"{request.FirstName} {request.LastName}";
                //data gửi lên server
                var user = new User
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Username = request.UserName,
                    Email = request.Email,
                    SearchName = StringHelper
                        .RemoveVietnameseDiacritics(displayName) //Bỏ dấu tiếng Việt
                        .ToLower() //chuyển thành chữ thường
                        .Trim(), //xóa khoảng trắng đầu và cuối
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
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi Signup");

                return StatusCode(500, ApiResponse.Fail(
                    message: $"{ex.Message}"
               ));
            }
        }

        //Api Login
        [HttpPost("login")]
        public async Task<ActionResult> Login(LoginDTORequest request)
        {
            try
            {
                //Lấy input & kiểm tra có bị thiếu field ko 
                if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.PassWord))
                {
                    return BadRequest(ApiResponse.Fail(
                        message: "Thiếu Usernanme hoặc Password"
                    ));
                }

                //Kiểm tra username có tồn tại hay ko 
                var user = await _usersCollection.Find(u => u.Username == request.UserName).FirstOrDefaultAsync();
                if (user == null)
                {
                    return Unauthorized(ApiResponse.Fail(
                        message: "Username hoặc Password không chính xác"
                    ));
                }

                //kiểm tra password có đúng hjay ko 
                var passwordCorrect = BCrypt.Net.BCrypt.Verify(request.PassWord, user.HashPassword);
                if (!passwordCorrect)
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
                await _sessionCollection.DeleteOneAsync(s => s.UserId == user.Id);

                await _sessionCollection.InsertOneAsync(new Session
                {
                    UserId = user.Id!,
                    Username = user.Username!,
                    RefreshToken = refreshToken,
                    ExpiresAt = refreshExpiry
                });

                // menthod Gắn refreshToken vào cookie
                Response.Cookies.Append(
                    "refreshToken",
                    refreshToken,
                    new CookieOptions
                    {
                        //Chống XSS, Chặn JavaScript đọc cookie.
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
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi gọi Login");

                return StatusCode(500, ApiResponse.Fail(
                    message: $"{ex.Message}"
                ));
            }
        }

        //Api SignOut
        [HttpPost("signout")]
        public async Task<ActionResult> LogOut()
        {
            try
            {
                // lấy refresh token từ cookie
                var token = Request.Cookies["refreshToken"];

                //Check token tồn tại trong session
                if (!string.IsNullOrEmpty(token))
                {
                    //xóa refresh token trong sesion
                    await _sessionCollection.DeleteManyAsync(
                       s => s.RefreshToken == token
                    );
                }
                // xóa refreshtoken trong cookie
                Response.Cookies.Delete("refreshToken");

                //Trả kết quả
                return NoContent();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi gọi Signout");
                return StatusCode(500, ApiResponse.Fail(
                    message: $"{ex.Message}"
                ));
            }
        }

        // [HttpPost("refresh")]
        // public IActionResult Refresh([FromBody] RefreshRequest request)
        // {
        //     var result = _tokenService.RefreshToken(request.RefreshToken);
        //     return result is null ? Unauthorized() : Ok(result);
        // }


    }
}
