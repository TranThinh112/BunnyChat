using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using BunnyChat.Models.Entities;
using System.Security.Cryptography;


namespace BunnyChat.Service
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        //Tạo AccessToken
        public string CreateAccessToken(User user)
        {
            //Tạo claims mã hóa data user gồm các trường Id, Username, Email
            var claims = new[]
            {
                new Claim("userId", user.Id!),
                // new Claim(ClaimTypes.Name, user.Username ?? ""),
                // new Claim(ClaimTypes.Email, user.Email ?? "")
            };

            //Tạo secret Key
            // "Symmetric" nghĩa là:    cùng 1 secret để ký     cùng secret đó để verify
            var key = new SymmetricSecurityKey(

                //Chuyển Chuổi thành byte
                Encoding.UTF8.GetBytes(
                    _configuration["JwtSettings:SecurityKey"]!
                )
            );

            //cấu hình chữ ký
            var creds = new SigningCredentials(
                key, SecurityAlgorithms.HmacSha256 //HMAC + SHA256
            );

            //tạo Token và Chuyển token hiện đang là Object thành String
            return new JwtSecurityTokenHandler()
                .WriteToken(
                    new JwtSecurityToken(
                        issuer: _configuration["JwtSettings:Issuer"],
                        audience: _configuration["JwtSettings:Audience"],
                        claims: claims,
                        expires: DateTime.UtcNow.AddMinutes(
                             Convert.ToDouble(_configuration["JwtSettings:AccessTokenExpirationMinutes"])
                            ),
                        signingCredentials: creds
                    )
                );
        }

        //Tạo refresh Token
        public string CreateRefreshToken()
        {
            var randomBytes = new byte[64];

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }

            return Convert.ToBase64String(randomBytes);
        }
    }
}