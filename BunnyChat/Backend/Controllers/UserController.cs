using System.Security.Cryptography.X509Certificates;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using BunnyChat.Service;
using BunnyChat.DTOs;
using BunnyChat.Models;
using BunnyChat.Helper;

namespace BunnyChat.Controllers

{
    // [Authorize]
    [ApiController]
    [Route("/api/users/")]
    public class UserController : ControllerBase
    {
        private readonly IMongoCollection<User> _usersCollection;

        private readonly IMongoCollection<Session> _sessionCollection;


        public UserController(MongoDbService mongoDbService)
        {
            _usersCollection = mongoDbService.Database.GetCollection<User>("users");
            _sessionCollection = mongoDbService.Database.GetCollection<Session>("sessions");

        }

        // hàm tìm user theo id
        private async Task<User?> FindUserById(String userId)
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

                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse.Fail("Token không hợp lệ"));
                }

                // đọc claim
                // foreach (var claim in User.Claims)
                // {
                //     Console.WriteLine($"{claim.Type}: {claim.Value}");
                // }

                // Console.WriteLine($"JWT UserId = {userId}");

                var session = await _sessionCollection
                    .Find(x => x.UserId == userId)
                    .FirstOrDefaultAsync();

                if (session == null)
                {
                    return Unauthorized(ApiResponse.Fail("Phiên đăng nhập đã hết hạn"));
                }


                Console.WriteLine($"userID: {session.UserId}");


                var user = await FindUserById(userId);

                if (user == null)
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
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.WriteLine("Lỗi khi lấy dữ liệu người dùng");
                return StatusCode(500, ApiResponse.Fail(
                    message: $"{ex.Message}"
                ));
            }
        }

        //API update Thong tin user đang login
        [Authorize]
        [HttpPatch("me")]
        public async Task<ActionResult> UpdateInformation(UserInformationDTORequest request)
        {
            try
            {
                // lấy userId từ JWT token
                var userId = GetUserId();

                // gọi hàm tìm user bằng id
                var user = await FindUserById(userId);

                if (user == null)
                {
                    return NotFound(ApiResponse.Fail(
                         message: "Người dùng không tồn tại"
                    ));
                }
                // gọi class NormalizeToInternational để hỗ trợ lưu Phone bỏ số 0 đầu và kèm mã vùng 
                var normalizedPhone =
                    PhoneHelper.NormalizeToInternational(request.Phone);

                //kiểm tra sđt có trùng hay ko ?
                if (!string.IsNullOrWhiteSpace(request.Phone))
                {
                    var duplicateUser = await _usersCollection
                        .Find(u =>
                            u.Phone == normalizedPhone &&
                            u.Id != userId)
                        .FirstOrDefaultAsync();

                    if (duplicateUser != null)
                    {
                        return Conflict(ApiResponse.Fail(
                            message: "Số điện thoại đã tồn tại"
                        ));
                    }
                }

                var firstName = request.FirstName ?? user.FirstName;
                var lastName = request.LastName ?? user.LastName;
                var nickname = request.Nickname ?? user.Nickname;
                var bio = request.Bio ?? user.Bio;
                var phone = request.Phone ?? user.Phone;
                var avatar = request.AvatarUrl ?? user.AvatarUrl;

                // tính toán displayname
                var displayName =
                    !string.IsNullOrWhiteSpace(nickname)
                        ? $"{firstName} {lastName} {nickname}"
                        : $"{firstName} {lastName}";

                // gán searchName = displayName
                var searchName = StringHelper
                    .RemoveVietnameseDiacritics(displayName) //Bỏ dấu tiếng Việt
                    .ToLower() //chuyển thành chữ thường
                    .Trim(); //xóa khoảng trắng đầu và cuối

                // updatedUser dữ liệu
                var update = Builders<User>.Update
                    .Set(x => x.FirstName, firstName)
                    .Set(x => x.LastName, lastName)
                    .Set(x => x.Nickname, nickname)
                    .Set(x => x.Bio, bio)
                    .Set(x => x.SearchName, searchName)
                    .Set(x => x.Phone, normalizedPhone)
                    .Set(x => x.AvatarUrl, avatar)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow);

                await _usersCollection.UpdateOneAsync(
                    x => x.Id == userId,
                    update
                );

                // update xong thì querry lại data để trả về data mới nhất
                var updatedUser = await FindUserById(userId);

                return Ok(ApiResponse.Success(
                    message: "Update thông tin thành công",
                    new
                    {
                        id = updatedUser.Id,
                        username = updatedUser.Username,
                        displayname = updatedUser.DisplayName,
                        email = updatedUser.Email,
                        phone = updatedUser.Phone,
                        updateAt = updatedUser.UpdatedAt
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
        public async Task<IActionResult> Search(string? q)
        {
            try
            {
                //kiểm tra querry
                if (string.IsNullOrWhiteSpace(q))
                {
                    return BadRequest(ApiResponse.Fail("Thiếu từ khóa tìm kiếm"));
                }

                var keyword = q.Trim();

                var keywordNoAccent = StringHelper
                    .RemoveVietnameseDiacritics(keyword) //Bỏ dấu tiếng Việt
                        .ToLower() //chuyển thành chữ thường
                        .Trim(); //xóa khoảng trắng đầu và cuối

                //tách từng chữ  // "tran thinh"
                    // => ["tran", "thinh"]
                var words = keywordNoAccent.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                // Tạo filter cho từng từ
                    // SearchName LIKE '%tran%'
                    // SearchName LIKE '%thinh%'
                var wordFilters = words.Select(word =>
                    Builders<User>.Filter.Regex(
                        x => x.SearchName,
                        new BsonRegularExpression(word, "i")
                    )
                );

                var finalFilter = Builders<User>.Filter.Or(
                    // tìm tương dối 
                    Builders<User>.Filter.Regex(x => x.Username, new BsonRegularExpression(keyword, "i")),

                    //tìm chắc chắn
                    Builders<User>.Filter.Eq(x => x.Email, keyword),
                    Builders<User>.Filter.Regex(x => x.FirstName, new BsonRegularExpression(keyword, "i")),
                    Builders<User>.Filter.Regex(x => x.LastName, new BsonRegularExpression(keyword, "i")),

                    // tìm theo searchName
                    Builders<User>.Filter.And(wordFilters)
                );

                //querry database
                var users = await _usersCollection.Find(finalFilter).ToListAsync();

                if (!users.Any())
                {
                    return NotFound(ApiResponse.Fail("Không tìm thấy user"));
                }

                //trả dữ liệu nếu querry thành công
                return Ok(ApiResponse.Success(
                    message: "Tìm thấy user",
                    data: users.Select(user => new
                    {
                        id = user.Id,
                        username = user.Username,
                        displayname = user.DisplayName,
                        email = user.Email,
                        createdAt = user.CreatedAt,
                        updatedAt = user.UpdatedAt
                    })
                ));
            }

            //trả về lỗi
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi tìm kiếm");
                return StatusCode(500, ApiResponse.Fail(
                    message: $"{ex.Message}"
                ));
            }
        }
    }
}
