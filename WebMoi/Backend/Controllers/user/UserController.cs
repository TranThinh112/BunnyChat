using System.Security.Cryptography.X509Certificates;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using WebMoi.Data;
using WebMoi.DTOs;
using WebMoi.Models.Entities;

namespace WebMoi.Controllers

{
    // [Authorize]
    [ApiController]
    [Route("users/")]
    public class UserController : ControllerBase
    {
        private readonly IMongoCollection<User> _usersCollection;

        public UserController(MongoDbService mongoDbService) 
        {
            _usersCollection = mongoDbService.Database.GetCollection<User>("users");
        }

        // hàm tìm user theo id
        private async Task<User?> FindUserById (String userId)
        {
            return await _usersCollection
                    .Find(x => x.Id == userId)
                    .FirstOrDefaultAsync();
        }

        //lấy UserId từ HttpContext.User sau khi JWT middleware verify token, được nhét vào Context.User
        private string? GetUserId()
        {
            return User.FindFirst("userId")?.Value;
        }

        //API lay toan bo user
        [AllowAnonymous]
        [HttpGet]
        public async Task<IEnumerable<User>> Get() //IEnumerable: danh sach cac user, Task: async, ActionResult: tra ve 200, 404, 500, có thể là map or list
        {   
            return await _usersCollection.Find(FilterDefinition<User>.Empty).ToListAsync();
        }

        //API láy dữ liệu user đang login
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            try
            {
                var userId = User.FindFirst("userId")?.Value;

            //đọc claim
            // foreach (var claim in User.Claims)
            // {
            //     Console.WriteLine($"{claim.Type}: {claim.Value}");
            // }

                var user = await FindUserById(userId);

                if( user == null)
                {
                    return NotFound(ApiResponse.Fail(
                        message: "Người dùng không tồn tại"
                    ));
                }

                Console.WriteLine(user == null ? "NULL" : user.Username);

                return Ok(ApiResponse.Success(
                    message: "Lấy thông tin thành công",
                    user
                ));
            }

            //trả về lỗi
            catch(Exception ex)
            {
                Console.WriteLine("Lỗi khi lấy dữ liệu người dùng");
                return StatusCode(500, ApiResponse.Fail(
                    message: $"{ex.Message}"
                ));
            }
        }

        //API update Thong tin user đang login
        [Authorize]
        [HttpPatch("me")]
        public async Task<ActionResult> UpdateInformation ( string id, UserInformationDTORequest request)
        {
            try{
                 var  duplicateUser = await _usersCollection.Find(u => u.Phone == request.Phone).FirstOrDefaultAsync();

                if (duplicateUser != null) { 
                    
                    return StatusCode(409,ApiResponse.Fail(
                      message: "Số điện thoại đã tồn tại" 
                    ));
                }

                // lấy userId từ JWT token
                var userId = GetUserId();

                // gọi hàm tìm user bằng id
                var user = await FindUserById(userId);

                if(user == null)
                {
                    return NotFound(ApiResponse.Fail(
                         message: "Người dùng không tồn tại"
                    ));
                }

                var firstName = request.FirstName ?? user.FirstName;
                var lastName = request.LastName ?? user.LastName;
                var nickname = request.Nickname ?? user.Nickname;
                var bio = request.Bio ?? user.Bio;
                var phone = request.Phone ?? user.Phone;
                var avatar = request.AvatarUrl ?? user.AvatarUrl;

                var displayname = $"{firstName} {lastName}";

                var update = Builders<User>.Update
                    .Set(x => x.FirstName, firstName)
                    .Set(x => x.LastName, lastName)
                    .Set(x => x.Nickname, nickname)
                    .Set(x => x.Bio, bio)
                    .Set(x => x.Phone, phone)
                    .Set(x => x.AvatarUrl, avatar)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow);
                
                await _usersCollection.UpdateOneAsync(
                    x => x.Id == id,
                    update
                );

                return Ok(ApiResponse.Success(
                    message: "Update thông tin thành công",
                    new
                    {
                        id = user.Id,
                        displayname = user.DisplayName,
                        email = user.Email,
                        updateAt = user.UpdatedAt
                        
                    }
                ));
            }
            catch
            {
                return StatusCode(500, ApiResponse.Fail(
                    message: "Lỗi khi update"
                ));
            }
        }

        //API serach username, email người khác
        [Authorize]
        [HttpGet("search")]
        public async Task<IActionResult> Search(string? username, string? email)
        {
            try
            {
                // thiếu query
                if (
                    string.IsNullOrWhiteSpace(username) &&
                    string.IsNullOrWhiteSpace(email))
                {
                    return BadRequest(ApiResponse.Fail(
                        message: "thiếu username hoặc email"
                    ));
                }

                var filters = new List<FilterDefinition<User>>();

                // search username
                if (!string.IsNullOrWhiteSpace(username))
                {
                    filters.Add(
                        Builders<User>.Filter.Regex(
                            x => x.Username,
                            new BsonRegularExpression(username, "i")
                        )
                    );
                }

                // search email
                if (!string.IsNullOrWhiteSpace(email))
                {
                    // thêm điều kiện query. VD search Email => Email == Hoa@gmail.com
                    filters.Add(
                        Builders<User>.Filter.Eq(x => x.Email, email)
                    );
                }

                // Gộp filter, chỉ cần match 1 điều kiện là lấy <=> OR trong SQL
                var finalFilter = Builders<User>.Filter.Or(filters);

                //querry data ? list = [] return false đi thẳng vào NotFound
                 var user = await _usersCollection
                    .Find(finalFilter)
                    .ToListAsync(); // lay tat ca match

                // checked có user hay không
                if (!user.Any())
                {
                    return NotFound(ApiResponse.Fail(
                        message: "Không tìm thấy user"
                    ));
                }
                
                return Ok(ApiResponse.Success(
                    message: "Tìm Thấy user",
                    data: user.Select(user => new
                    {
                        id = user.Id,
                        username = user.Username,
                        displayname = user.DisplayName,
                        email = user.Email,
                        password = user.HashPassword,
                        createdAt = user.CreatedAt,
                        updatedAt = user.UpdatedAt
                    })
                ));
            }
            
            //trả về lỗi
            catch(Exception ex)
            {   
                Console.WriteLine( "Lỗi khi tìm kiếm");
                return StatusCode(500, ApiResponse.Fail(
                    message: $"{ex.Message}"
                ));
            }
        }
    }
}
