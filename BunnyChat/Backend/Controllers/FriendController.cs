using BunnyChat.DTOs;
using BunnyChat.Backend.Hubs;
using BunnyChat.Models;
using BunnyChat.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
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
        private readonly IHubContext<ChatHub> _chatHub;

        public FriendController(MongoDbService mongoDbService, IHubContext<ChatHub> chatHub)
        {
            _friendCollection = mongoDbService.Database.GetCollection<Friend>("friends");
            _userCollection = mongoDbService.Database.GetCollection<User>("users");
            _chatHub = chatHub;
        }

        // Lấy userId từ claim trong access token.
        private string? GetUserId()
        {
            return User.FindFirst("userId")?.Value;
        }

        // Tạo filter tìm quan hệ giữa hai user, không phân biệt ai gửi lời mời.
            // kiểm tra hai người đã từng có quan hệ bạn bè/lời mời nào với nhau hay chưa, bất kể ai là người gửi trước
        private FilterDefinition<Friend> BuildFriendPairFilter(string userA, string userB)
        {
            return Builders<Friend>.Filter.Or( //tương đương or trong SQL => ghép 2 đoạn AND thành 1 câu truy vấn hoàn chỉnh
            // SenderId A = AND ReceiveId = B
                Builders<Friend>.Filter.And(
                    Builders<Friend>.Filter.Eq(x => x.SenderId, userA),
                    Builders<Friend>.Filter.Eq(x => x.ReceiveId, userB)
                ),
            // SenderId B = AND ReceiveId = A

                Builders<Friend>.Filter.And(
                    Builders<Friend>.Filter.Eq(x => x.SenderId, userB),
                    Builders<Friend>.Filter.Eq(x => x.ReceiveId, userA)
                )
            );
        }

        // Format res user ngắn gọn cho frontend.
        private object FormatUser(User user)
        {
            return new
            {
                id = user.Id,
                userName = user.Username,
                displayname = user.DisplayName,
                avatarUrl = user.AvatarUrl
            };
        }

        // API cũ: giữ nguyên hành vi trả toàn bộ document friends.
        [AllowAnonymous]
        [HttpGet]
        public async Task<IEnumerable<Friend>> Get()
        {
            return await _friendCollection.Find(FilterDefinition<Friend>.Empty).ToListAsync();
        }


//lấu danh sách bạn bè của /me
        [HttpGet("/api/friends")]
        public async Task<IActionResult> GetAllFriends()
        {
            try
            {
                //lấy user hiện tại
                var currentUserId = GetUserId();

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return Unauthorized(ApiResponse.Fail("Token khong hop le"));

                //đã là friends thì status là aceppted và sender = u1
                var friendships = await _friendCollection
                    .Find(x =>
                        x.Status == FriendStatus.Accepted &&
                        (x.SenderId == currentUserId || x.ReceiveId == currentUserId))
                    .ToListAsync();

                // lây id của người bạn 
                // Trường hợp 1
                    // {    "senderId":"u1",  "receiveId":"u2"  }
                    // u1 là mình.  => bạn bè là: u2

                // Trường hợp 2
                    // {  "senderId":"u3",   "receiveId":"u1" }
                    // u1 là mình.  => bạn bè là: u3

                var friendIds = friendships
                    .Select(x => x.SenderId == currentUserId ? x.ReceiveId : x.SenderId)
                    //sau Select là [ "u2", "u3" ]
                    .Distinct() //loại trùng
                    .ToList();

                //không có bạn bè => trả mảng rỗng
                if (!friendIds.Any())
                    return Ok(ApiResponse.Success("Lấy danh sách bạn bè thành công", new List<object>()));

                //lấy thông tin user bằng Id
                var users = await _userCollection
                    .Find(x => friendIds.Contains(x.Id!))
                    .ToListAsync(); //ToListAsync: chuyển thành list

                //trả res 200
                return Ok(ApiResponse.Success(
                    "Lấy danh sách bạn bè thành công",
                    users.Select(FormatUser)
                ));
            }
            //bắt lỗi
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi lấy danh sách bạn bè");
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        // API lấy danh sách lời mời đã gửi và đã nhận. của người đang đăng nhập
        [HttpGet("/api/friends/requests")]
        public async Task<IActionResult> GetFriendRequests()
        {
            try
            {
                //lấy userid từ token
                var currentUserId = GetUserId();

                // tất cả lời mời Pending có liên quan tới A, A là ng nhận hoặc ng gửi
                var requests = await _friendCollection
                    .Find(x =>
                        x.Status == FriendStatus.Pending &&
                        (x.SenderId == currentUserId || x.ReceiveId == currentUserId))
                    .ToListAsync();

                // gom tất cả userId xuất hiện trong các lời mời
                var userIds = requests
                    .SelectMany(x => new[] { x.SenderId, x.ReceiveId })
                    .Distinct()
                    .ToList();

                //tìm thông tin user từ collection
                var users = await _userCollection
                    .Find(x => userIds.Contains(x.Id!))
                    .ToListAsync();

                //format lại từng lời mời. Vd A đang đăng nhập. Nếu lời mời là: A -> B => otherId = B.
                    // Nếu lời mời là: C -> A => otherId = C.
                    // Nếu mình là người gửi => lấy người nhận,
                object FormatRequest(Friend friend)
                {
                    // curent = A, Sender = A => ortherId = Receiver
                    //current =A, Sender = C => ortherId = Sender
                    var otherId = friend.SenderId == currentUserId
                        ? friend.ReceiveId
                        : friend.SenderId;

                    //tìm thông tin user còn lại
                    var otherUser = users.FirstOrDefault(x => x.Id == otherId);

                    return new
                    {
                        id = friend._Id,
                        message = friend.Message,
                        createdAt = friend.CreatedAt,
                        status = friend.Status.ToString(),
                        user = otherUser == null ? null : FormatUser(otherUser)
                    };
                }

                return Ok(ApiResponse.Success("Lấy lời mời kết bạn thành công", new
                {
                    // chia res thành lời mời gửi và lời mời nhận
                    sent = requests
                        .Where(x => x.SenderId == currentUserId)
                        .Select(FormatRequest),
                    received = requests
                        .Where(x => x.ReceiveId == currentUserId)
                        .Select(FormatRequest)
                }));
            }
            //bắt lỗi
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi lấy lời mời kết bạn");
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        // API gửi lời mời kết bạn.
        [HttpPost("/api/friends/requests")]
        public async Task<IActionResult> SendFriendRequest(FriendDTORequest request)
        {
            try
            {
                //lấy userId từ claim trong payload của Token
                var currentUserId = GetUserId();

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return Unauthorized(ApiResponse.Fail("Token khong hop le"));

                // thiếu người nhận
                if (string.IsNullOrWhiteSpace(request.ReceiveId))
                    return BadRequest(ApiResponse.Fail("Thiếu người nhận lời mời"));

                //ko thể gửi kết bạn cho chính mình
                // kiêmr tra Id lấy đc từ claim, nếu trùng UserId với body trong requets thì không cho gửi requets
                if (currentUserId == request.ReceiveId)
                    return BadRequest(ApiResponse.Fail("Không thể gửi lời mời kết bạn cho chính mình"));

                // tìm UserId của người nhận lời mời kết bạn
                var receiver = await _userCollection
                    .Find(x => x.Id == request.ReceiveId)
                    .FirstOrDefaultAsync();

                //kiểm tra người nhận lời mời có tồn tại ko
                if (receiver == null)
                    return NotFound(ApiResponse.Fail("Người dùng không tồn tại"));

                //Kiểm tra giữa 2 user này đã từng có quan hệ chưa:
                    // Đã gửi lời mời 
                    // Đã là bạn bè
                    // Đã có record friend trong DB
                var oldFriend = await _friendCollection
                    .Find(BuildFriendPairFilter(currentUserId, request.ReceiveId))
                    .FirstOrDefaultAsync();

                //nếu oldFriend == null thì ko có quan hệ bạn bè
                if (oldFriend != null)
                    return Conflict(ApiResponse.Fail("Đã tồn tại lời mời hoặc quan hệ bạn bè"));

                //tạo lời mời kết bạn mới
                var friend = new Friend
                {
                    _Id = ObjectId.GenerateNewId().ToString(),
                    SenderId = currentUserId,
                    ReceiveId = request.ReceiveId,
                    Message = request.Message,
                    Status = FriendStatus.Pending, //trạng thái ban đầu là pending: 0 do dùng enum
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // lưu vào database
                await _friendCollection.InsertOneAsync(friend);

                // Lấy thông tin người gửi để gửi realtime cho người nhận.
                var sender = await _userCollection
                    .Find(x => x.Id == currentUserId)
                    .FirstOrDefaultAsync();

                //SignalR gửi sự kiện realtime cho ng nhận
                await _chatHub.Clients
                    .Group(ChatHub.UserGroup(request.ReceiveId))
                    .SendAsync("friend-request-created", new //tên sự kiện đẻ FE lắng nghe
                    {
                        id = friend._Id,
                        message = friend.Message,
                        createdAt = friend.CreatedAt,
                        user = sender == null ? null : FormatUser(sender)
                    });

                //trả res thành công
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

        // API chấp nhận kết bạn.
        [HttpPost("/api/friends/requests/{requestId}/accept")]
        public async Task<IActionResult> AcceptFriendRequest(string requestId)
        {
            try
            {
                var currentUserId = GetUserId();

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return Unauthorized(ApiResponse.Fail("Token không hợp lệ"));

                //tìm lời mời kết bạn
                var request = await _friendCollection
                    .Find(x => x._Id == requestId)
                    .FirstOrDefaultAsync();

                // kiểm tra toonff tại
                if (request == null)
                    return NotFound(ApiResponse.Fail("Không tìm thấy lời mời kết bạn"));

                // kiểm tra quyền. A -> B thì chỉ B đc quyền chấp nhận
                if (request.ReceiveId != currentUserId)
                    return Forbid();

                //chuyển trạng thái pending -> accepted
                request.Status = FriendStatus.Accepted;
                // cập nhật thời gian 
                request.UpdatedAt = DateTime.UtcNow;

                // /thay thế dữ liệu cũ lên database
                await _friendCollection.ReplaceOneAsync(
                    x => x._Id == requestId,
                    request
                );

                //tìm thông tin 2 user
                var sender = await _userCollection
                    .Find(x => x.Id == request.SenderId)
                    .FirstOrDefaultAsync();

                var receiver = await _userCollection
                    .Find(x => x.Id == currentUserId)
                    .FirstOrDefaultAsync();

                //gửi signalR, báo cho A B đã chấp nhận kết bạn
                await _chatHub.Clients
                    .Group(ChatHub.UserGroup(request.SenderId))
                    .SendAsync("friend-request-accepted", new //event để kết nối
                    {
                        requestId,
                        user = receiver == null ? null : FormatUser(receiver)
                    });

                //trá kết quả cho B đã chấp nhận kết bạn, trả về bạn data của new friend
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

        // API từ chối kết bạn.
        [HttpPost("/api/friends/requests/{requestId}/decline")]
        public async Task<IActionResult> DeclineFriendRequest(string requestId)
        {
            try
            {
                var currentUserId = GetUserId();

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return Unauthorized(ApiResponse.Fail("Token không hợp lệ"));

                //tìm lời mời
                var request = await _friendCollection
                    .Find(x => x._Id == requestId)
                    .FirstOrDefaultAsync();

                // kiểm tra tồn tại
                if (request == null)
                    return NotFound(ApiResponse.Fail("Không tìm thấy lời mời kết bạn"));

                // chekc quyền. Chỉ người nhận mới đc từ chối
                if (request.ReceiveId != currentUserId)
                    return Forbid();

                // xóa lời mời trên data
                await _friendCollection.DeleteOneAsync(x => x._Id == requestId);

                //thông báo realtime bằng SignalR
                await _chatHub.Clients
                    .Group(ChatHub.UserGroup(request.SenderId))
                    .SendAsync("friend-request-declined", new //event để FE bên A có thể thấy
                    {
                        requestId
                    });

                //res 204
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
