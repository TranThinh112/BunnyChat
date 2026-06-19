using BunnyChat.DTOs;
using BunnyChat.Models;
using BunnyChat.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BunnyChat.Controllers
{
    [Authorize]
    [ApiController]
    [Route("/api/friend/")]
    public class FriendController : ControllerBase
    {
        private readonly IMongoCollection<Friend> _friendCollection;
        private readonly IMongoCollection<User> _userCollection;

        public FriendController(MongoDbService mongoDbService)
        {
            _friendCollection = mongoDbService.Database.GetCollection<Friend>("friends");
            _userCollection = mongoDbService.Database.GetCollection<User>("users");
        }

        // Lấy userId từ claim trong access token.
        private string? GetUserId()
        {
            return User.FindFirst("userId")?.Value;
        }

        // Tạo filter tìm quan hệ giữa hai user, không phân biệt ai gửi lời mời.
        private FilterDefinition<Friend> BuildFriendPairFilter(string userA, string userB)
        {
            return Builders<Friend>.Filter.Or(
                Builders<Friend>.Filter.And(
                    Builders<Friend>.Filter.Eq(x => x.SenderId, userA),
                    Builders<Friend>.Filter.Eq(x => x.ReceiveId, userB)
                ),
                Builders<Friend>.Filter.And(
                    Builders<Friend>.Filter.Eq(x => x.SenderId, userB),
                    Builders<Friend>.Filter.Eq(x => x.ReceiveId, userA)
                )
            );
        }

        // Format user ngắn gọn cho frontend.
        private object FormatUser(User user)
        {
            return new
            {
                id = user.Id,
                username = user.Username,
                displayname = user.DisplayName,
                avatarUrl = user.AvatarUrl
            };
        }

        // API cũ: giữ nguyên hành vi trả toàn bộ document friends.
        [HttpGet]
        public async Task<IEnumerable<Friend>> Get()
        {
            return await _friendCollection.Find(FilterDefinition<Friend>.Empty).ToListAsync();
        }

        // API mới: gửi lời mời kết bạn.
        [HttpPost("/api/friends/requests")]
        public async Task<IActionResult> SendFriendRequest(FriendDTORequest request)
        {
            try
            {
                var currentUserId = GetUserId();

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return Unauthorized(ApiResponse.Fail("Token không hợp lệ"));

                if (string.IsNullOrWhiteSpace(request.ReceiveId))
                    return BadRequest(ApiResponse.Fail("Thiếu người nhận lời mời"));

                if (currentUserId == request.ReceiveId)
                    return BadRequest(ApiResponse.Fail("Không thể gửi lời mời kết bạn cho chính mình"));

                var receiver = await _userCollection
                    .Find(x => x.Id == request.ReceiveId)
                    .FirstOrDefaultAsync();

                if (receiver == null)
                    return NotFound(ApiResponse.Fail("Người dùng không tồn tại"));

                var oldFriend = await _friendCollection
                    .Find(BuildFriendPairFilter(currentUserId, request.ReceiveId))
                    .FirstOrDefaultAsync();

                if (oldFriend != null)
                    return Conflict(ApiResponse.Fail("Đã tồn tại lời mời hoặc quan hệ bạn bè"));

                var friend = new Friend
                {
                    _Id = ObjectId.GenerateNewId().ToString(),
                    SenderId = currentUserId,
                    ReceiveId = request.ReceiveId,
                    Message = request.Message,
                    Status = FriendStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _friendCollection.InsertOneAsync(friend);

                return StatusCode(201, ApiResponse.Success(
                    "Gửi lời mời kết bạn thành công",
                    friend
                ));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi gửi lời mời kết bạn");
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        // API mới: lấy danh sách bạn bè đã chấp nhận.
        [HttpGet("/api/friends")]
        public async Task<IActionResult> GetAllFriends()
        {
            try
            {
                var currentUserId = GetUserId();

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return Unauthorized(ApiResponse.Fail("Token không hợp lệ"));

                var friendships = await _friendCollection
                    .Find(x =>
                        x.Status == FriendStatus.Accepted &&
                        (x.SenderId == currentUserId || x.ReceiveId == currentUserId))
                    .ToListAsync();

                var friendIds = friendships
                    .Select(x => x.SenderId == currentUserId ? x.ReceiveId : x.SenderId)
                    .Distinct()
                    .ToList();

                if (!friendIds.Any())
                    return Ok(ApiResponse.Success("Lấy danh sách bạn bè thành công", new List<object>()));

                var users = await _userCollection
                    .Find(x => friendIds.Contains(x.Id!))
                    .ToListAsync();

                return Ok(ApiResponse.Success(
                    "Lấy danh sách bạn bè thành công",
                    users.Select(FormatUser)
                ));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi lấy danh sách bạn bè");
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        // API mới: lấy lời mời đã gửi và đã nhận.
        [HttpGet("/api/friends/requests")]
        public async Task<IActionResult> GetFriendRequests()
        {
            try
            {
                var currentUserId = GetUserId();

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return Unauthorized(ApiResponse.Fail("Token không hợp lệ"));

                var requests = await _friendCollection
                    .Find(x =>
                        x.Status == FriendStatus.Pending &&
                        (x.SenderId == currentUserId || x.ReceiveId == currentUserId))
                    .ToListAsync();

                var userIds = requests
                    .SelectMany(x => new[] { x.SenderId, x.ReceiveId })
                    .Distinct()
                    .ToList();

                var users = await _userCollection
                    .Find(x => userIds.Contains(x.Id!))
                    .ToListAsync();

                object FormatRequest(Friend friend)
                {
                    var otherId = friend.SenderId == currentUserId
                        ? friend.ReceiveId
                        : friend.SenderId;

                    var otherUser = users.FirstOrDefault(x => x.Id == otherId);

                    return new
                    {
                        id = friend._Id,
                        message = friend.Message,
                        createdAt = friend.CreatedAt,
                        user = otherUser == null ? null : FormatUser(otherUser)
                    };
                }

                return Ok(ApiResponse.Success("Lấy lời mời kết bạn thành công", new
                {
                    sent = requests
                        .Where(x => x.SenderId == currentUserId)
                        .Select(FormatRequest),
                    received = requests
                        .Where(x => x.ReceiveId == currentUserId)
                        .Select(FormatRequest)
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi lấy lời mời kết bạn");
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        // API mới: chấp nhận lời mời kết bạn.
        [HttpPost("/api/friends/requests/{requestId}/accept")]
        public async Task<IActionResult> AcceptFriendRequest(string requestId)
        {
            try
            {
                var currentUserId = GetUserId();

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return Unauthorized(ApiResponse.Fail("Token không hợp lệ"));

                var request = await _friendCollection
                    .Find(x => x._Id == requestId)
                    .FirstOrDefaultAsync();

                if (request == null)
                    return NotFound(ApiResponse.Fail("Không tìm thấy lời mời kết bạn"));

                if (request.ReceiveId != currentUserId)
                    return Forbid();

                request.Status = FriendStatus.Accepted;
                request.UpdatedAt = DateTime.UtcNow;

                await _friendCollection.ReplaceOneAsync(
                    x => x._Id == requestId,
                    request
                );

                var sender = await _userCollection
                    .Find(x => x.Id == request.SenderId)
                    .FirstOrDefaultAsync();

                return Ok(ApiResponse.Success("Chấp nhận lời mời kết bạn thành công", new
                {
                    newFriend = sender == null ? null : FormatUser(sender)
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi chấp nhận lời mời kết bạn");
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        // API mới: từ chối lời mời kết bạn.
        [HttpPost("/api/friends/requests/{requestId}/decline")]
        public async Task<IActionResult> DeclineFriendRequest(string requestId)
        {
            try
            {
                var currentUserId = GetUserId();

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return Unauthorized(ApiResponse.Fail("Token không hợp lệ"));

                var request = await _friendCollection
                    .Find(x => x._Id == requestId)
                    .FirstOrDefaultAsync();

                if (request == null)
                    return NotFound(ApiResponse.Fail("Không tìm thấy lời mời kết bạn"));

                if (request.ReceiveId != currentUserId)
                    return Forbid();

                await _friendCollection.DeleteOneAsync(x => x._Id == requestId);

                return NoContent();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi từ chối lời mời kết bạn");
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }
    }
}
