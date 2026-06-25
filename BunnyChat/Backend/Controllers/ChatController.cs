using BunnyChat.DTOs;
using BunnyChat.Backend.Hubs;
using BunnyChat.Helper;
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
    [Route("/api/chat")]
    public class ChatController : ControllerBase
    {
        private readonly IMongoCollection<Conversation> _conversationCollection;
        private readonly IMongoCollection<Message> _messageCollection;
        private readonly IMongoCollection<User> _userCollection;
        private readonly IHubContext<ChatHub> _chatHub;

        //các collection 
        public ChatController(MongoDbService mongoDbService, IHubContext<ChatHub> chatHub)
        {
            _conversationCollection = mongoDbService.Database.GetCollection<Conversation>("conversations");
            _messageCollection = mongoDbService.Database.GetCollection<Message>("messages");
            _userCollection = mongoDbService.Database.GetCollection<User>("users");
            _chatHub = chatHub;
        }

        // Lấy userId từ claim trong access token.
        private string? GetUserId()
        {
            return User.FindFirst("userId")?.Value;
        }

        // Tìm conversation theo id.
        private async Task<Conversation?> FindConversationById(string conversationId)
        {
            return await _conversationCollection
                .Find(x => x.Id == conversationId)
                .FirstOrDefaultAsync();
        }

        // Tìm user theo id để kiểm tra thành viên có tồn tại không.
        private async Task<User?> FindUserById(string userId)
        {
            return await _userCollection
                .Find(x => x.Id == userId)
                .FirstOrDefaultAsync();
        }

        // Format response của conversation để frontend render card dễ hơn
        private async Task<object> FormatConversation(Conversation conversation, string currentUserId)
        {
            //lấy id của các tv trong nhóm
            var participantIds = ConversationHelper.GetParticipantIds(conversation);
            var users = await _userCollection
                .Find(x => participantIds.Contains(x.Id!))
                .ToListAsync();

            var members = users.Select(user => new
            {
                id = user.Id,
                userName = user.Username,
                displayname = user.DisplayName,
                avatarUrl = user.AvatarUrl
            }).ToList();

            // lấy người còn lại trong cuộc trò chuyện 1-1.
            var directUser = users.FirstOrDefault(x => x.Id != currentUserId);
            var isGroup = conversation.Type == "group";
            var displayName = isGroup
                ? conversation.Group?.Name
                : directUser?.DisplayName;

            return new
            {
                id = conversation.Id,
                type = conversation.Type,
                name = displayName,
                directUserId = directUser?.Id,
                userName = directUser?.Username,
                avatarUrl = directUser?.AvatarUrl,
                memberCount = participantIds.Count,
                members,
                createdAt = conversation.CreatedAt,
                updatedAt = conversation.UpdatedAt,
                lastMessage = conversation.LastMessage?.Content, // nộ dung tn cuối
                lastMessageAt = conversation.LastMessageAt, // thời gian tin nhắn cuối

                // Nếu Dictionary có currentUserId thì lấy số tin chưa đọc của user đó, ngược lại trả về 0.
                unreadCount = conversation.UnreadCounts.ContainsKey(currentUserId)
                    ? conversation.UnreadCounts[currentUserId]
                    : 0
            };
        }

        // Format message kèm thông tin người gửi để frontend hiển thị avatar cạnh tin nhắn.
        private async Task<object> FormatMessage(Message message, string currentUserId)
        {
            var sender = await FindUserById(message.SenderId);

            return new
            {
                id = message.Id,
                conversationId = message.ConversationId,
                senderId = message.SenderId,
                senderName = sender?.DisplayName,
                senderUserName = sender?.Username,
                senderAvatarUrl = sender?.AvatarUrl,
                content = message.Content,
                imgUrl = message.ImgUrl,
                isMine = message.SenderId == currentUserId,
                createdAt = message.CreatedAt,
                updatedAt = message.UpdatedAt
            };
        }

        // Tạo direct chat hoặc group chat mới.
        [HttpPost]
        public async Task<IActionResult> CreateConversation(CreateConversationDTORequest request)
        {
            try
            {
                var currentUserId = GetUserId();

                if (request.MemberIds == null || !request.MemberIds.Any())
                    return BadRequest(ApiResponse.Fail("Danh sách thành viên không được trống"));

                // chuẩn hóa type
                var type = request.Type.Trim().ToLower();
                // lọc member id, bỏ id trống, trùng, chuyển về list
                var memberIds = request.MemberIds
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();

                // kiểm tra loại conversation
                if (type != "direct" && type != "group")
                    return BadRequest(ApiResponse.Fail("Loại conversation không hợp lệ"));

                // ko đc thiếu tên nhóm
                if (type == "group" && string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(ApiResponse.Fail("Tên nhóm là bắt buộc"));


                if (type == "direct")
                {
                    // Không được gửi 0 người   Không được gửi nhiều hơn 1 người    Không được tự chat với chính mình
                    if (memberIds.Count != 1 || memberIds.First() == currentUserId)
                        return BadRequest(ApiResponse.Fail("Direct chat cần đúng một người nhận khác user hiện tại"));

                    // kiểm tra direct chat tồn tại chưa
                    var participantId = memberIds.First();

                    // tìm coversation chứa cả 2 user 
                    var oldConversation = await _conversationCollection
                        .Find(x =>
                            x.Type == "direct" &&
                            x.Participants.Any(p => p.UserId == currentUserId) &&
                            x.Participants.Any(p => p.UserId == participantId))
                        .FirstOrDefaultAsync();

                    if (oldConversation != null)
                    {
                        return Ok(ApiResponse.Success(
                            "Conversation đã tồn tại",
                            await FormatConversation(oldConversation, currentUserId)
                        ));
                    }
                }

                if (type == "group" && memberIds.Count < 1)
                    return BadRequest(ApiResponse.Fail("Nhóm cần ít nhất một thành viên ngoài user hiện tại"));

                // Gộp current user vào danh sách member
                var allMemberIds = new List<string> { currentUserId }
                    .Concat(memberIds)
                    .Distinct()
                    .ToList();

                //kiểm tra user có tồn tại ko
                foreach (var memberId in allMemberIds)
                {
                    var user = await FindUserById(memberId);
                    if (user == null)
                        return NotFound(ApiResponse.Fail($"Không tìm thấy user {memberId}"));
                }

                //tạo obeject để lưu vào môngo
                var conversation = new Conversation
                {
                    Type = type,
                    Participants = allMemberIds
                        .Select(x => new Participant { UserId = x })
                        .ToList(),
                    Group = type == "group"
                        ? new GroupInfo
                        {
                            Name = request.Name?.Trim(),
                            CreatedBy = currentUserId
                        }
                        : null,
                    LastMessageAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                //tạo số tin chưa đọc
                conversation.UnreadCounts = allMemberIds.ToDictionary(x => x, _ => 0);

                await _conversationCollection.InsertOneAsync(conversation);

                // Gửi realtime cho từng thành viên(SingalR )
                foreach (var memberId in allMemberIds)
                {
                    var memberConversation = await FormatConversation(conversation, memberId);

                    await _chatHub.Clients
                        .Group(ChatHub.UserGroup(memberId))
                        .SendAsync("new-conversation", memberConversation);
                }

                return StatusCode(201, ApiResponse.Success(
                    "Tạo conversation thành công",
                    await FormatConversation(conversation, currentUserId)
                ));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi tạo conversation");
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        // Láº¥y toàn bộ conversation của user hiện tại
        [HttpGet]
        // Lấy toàn bộ conversation của user hiện tại để render sidebar.
        public async Task<IActionResult> GetConversations()
        {
            try
            {
                var currentUserId = GetUserId();
                //truy vaansn data
                var conversations = await _conversationCollection
                    .Find(x => x.Participants.Any(p => p.UserId == currentUserId)) //Tìm tất cả conversation có chứa user hiện tại trong danh sách Participants.
                    .SortByDescending(x => x.LastMessageAt) //Conversation nào có tin nhắn mới nhất thì lên đầu.
                    .ThenByDescending(x => x.UpdatedAt)
                    .ToListAsync();

                //format data cho FE
                var data = new List<object>();

                foreach (var conversation in conversations)
                {
                    data.Add(await FormatConversation(conversation, currentUserId));
                }

                return Ok(ApiResponse.Success("Lây danh sách conversation thành công", data));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi lây conversations");
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        // Lấy danh sÃ¡ch group chat của user hiệnh tại, dÃ¹ng cho sidebar frontend.
        // [HttpGet("groups")]
        // public async Task<IActionResult> GetGroups()
        // {
        //     try
        //     {
        //         var currentUserId = GetUserId();

        //         var groups = await _conversationCollection
        //             .Find(x => x.Type == "group" && x.Participants.Any(p => p.UserId == currentUserId))
        //             .SortByDescending(x => x.LastMessageAt)
        //             .ThenByDescending(x => x.UpdatedAt)
        //             .ToListAsync();

        //         var data = new List<object>();

        //         foreach (var group in groups)
        //         {
        //             data.Add(await FormatConversation(group, currentUserId));
        //         }

        //         return Ok(ApiResponse.Success("Láº¥y danh sÃ¡ch nhÃ³m thÃ nh cÃ´ng", data));
        //     }
        //     catch (Exception ex)
        //     {
        //         Console.WriteLine("Lá»—i khi láº¥y danh sÃ¡ch nhÃ³m");
        //         return StatusCode(500, ApiResponse.Fail(ex.Message));
        //     }
        // }

        // Thêm thành viên mới vào nhóm chat. Nếu user đã ở trong nhóm thì không thêm trùng.


        [HttpPost("{conversationId}/members")]
        // Thêm các user chưa có trong nhóm vào danh sách thành viên. Nhóm đã đc tạo
        public async Task<IActionResult> AddGroupMembers(string conversationId, AddGroupMembersDTORequest request)
        {
            try
            {
                var currentUserId = GetUserId();

                //check thieeu data
                if (request.MemberIds == null || !request.MemberIds.Any())
                    return BadRequest(ApiResponse.Fail("Danh sách thành viên ko để trống"));

                //tìm coversation
                var conversation = await FindConversationById(conversationId);

                if (conversation == null)
                    return NotFound(ApiResponse.Fail("Conversation không tồn tại"));

                //kiểm tra có phải groyup ko ? 
                if (conversation.Type != "group")
                    return BadRequest(ApiResponse.Fail("Chỉ có thể thêm thành viên vào nhÃ³m chat"));

                // Kiểm tra người gọi có trong nhóm không
                if (!ConversationHelper.IsParticipant(conversation, currentUserId))
                    return Forbid(); //403: ko có quyền

                //lấy membver4 cũ
                var oldMemberIds = ConversationHelper.GetParticipantIds(conversation);

                // lọc member mới
                var newMemberIds = request.MemberIds
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .Where(x => !oldMemberIds.Contains(x)) // loại mem cũ
                    .ToList();

                // chặn việc thêm trùng thành viên vào nhóm. Các thành viên đã có trong gr => ko thêm ai đc => Trả 409
                if (!newMemberIds.Any())
                    return Conflict(ApiResponse.Fail("Các user này đã thuộc nhóm chat"));

                // kiểm tra user có tồn tại ko
                foreach (var memberId in newMemberIds)
                {
                    var user = await FindUserById(memberId);
                    if (user == null)
                        return NotFound(ApiResponse.Fail($"Ko tìm thây user {memberId}"));
                }

                //thêm vào ds thành viên
                foreach (var memberId in newMemberIds)
                {
                    conversation.Participants.Add(new Participant
                    {
                        UserId = memberId,
                        JoinedAt = DateTime.UtcNow
                    });

                    // unread cho thành viên mới, vì mới vào => =0
                    if (!conversation.UnreadCounts.ContainsKey(memberId))
                        conversation.UnreadCounts[memberId] = 0;
                }

                //update thời gian 
                conversation.UpdatedAt = DateTime.UtcNow;

                //update lên data
                await _conversationCollection.ReplaceOneAsync(
                    x => x.Id == conversation.Id,
                    conversation
                );

                // gửi realtime
                var allMemberIds = ConversationHelper.GetParticipantIds(conversation);
                // vòng lặp để gửi realtime cho từng id trong gr
                foreach (var memberId in allMemberIds)
                {
                    await _chatHub.Clients
                        .Group(ChatHub.UserGroup(memberId))
                        .SendAsync("new-conversation", await FormatConversation(conversation, memberId));
                }

                return Ok(ApiResponse.Success(
                    "Thêm thành viên thành công",
                    await FormatConversation(conversation, currentUserId)
                ));
            }
            catch (Exception ex)
            {
                Console.WriteLine("lỗi khi thêm thành viên vào nhóm");
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        // Lấy tin nhắn theo conversation, hỗ trợ phân trang bằng cursor createdAt.
        [HttpGet("{conversationId}/messages")]
        public async Task<IActionResult> GetMessages(string conversationId, int limit = 50, DateTime? cursor = null) // lấy 50 tn
        {
            try
            {
                var currentUserId = GetUserId();

                //tìm conversationm theo id
                var conversation = await FindConversationById(conversationId);

                //check tồn tại
                if (conversation == null)
                    return NotFound(ApiResponse.Fail("Conversation không tồn tại "));

                // check quyền, Chỉ thành viên trong conversation mới được xem tin nhắn.
                if (!ConversationHelper.IsParticipant(conversation, currentUserId))
                    return Forbid();

                //tạo filter để querry, chỉ lấy tn thuộc conver này
                var filter = Builders<Message>.Filter.Eq(x => x.ConversationId, conversationId);

                // Nếu có cursor thì lấy tin cũ hơn. Lt: Less than: nhỏ hơn
                if (cursor.HasValue)
                {
                    filter &= Builders<Message>.Filter.Lt(x => x.CreatedAt, cursor.Value);
                }

                //giới hạn tn trả về
                var pageSize = Math.Clamp(limit, 1, 100);

                //querrt tn
                var messages = await _messageCollection
                    .Find(filter)
                    .SortByDescending(x => x.CreatedAt)
                    .Limit(pageSize + 1) // lấy +1 để bt còn trang sau ko.
                    .ToListAsync();

                //tạo next cursor
                DateTime? nextCursor = null;

                //nếu lấy dư đc 1 tin => lấy time của tin dư làm nextCursor, xóa tin dư khỏi res
                if (messages.Count > pageSize)
                {
                    nextCursor = messages.Last().CreatedAt;
                    messages.RemoveAt(messages.Count - 1);
                }

                var data = new List<object>();

                //đảo thứ tự cũ lên mới xuống
                foreach (var message in messages.OrderBy(x => x.CreatedAt))
                {
                    //format cho từng message
                    data.Add(await FormatMessage(message, currentUserId));
                }

                return Ok(ApiResponse.Success(
                    "Lấy tin nhắn thành công",
                    new
                    {
                        messages = data,
                        nextCursor
                    }
                ));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi lấy tin nhắn");
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        // Gửi tin nhắn vào conversation
        [HttpPost("{conversationId}/messages")]
        // Gửi tin nhắn mới vào conversation và phát realtime qua SignalR.
        public async Task<IActionResult> SendMessage(string conversationId, SendMessageDTORequest request)
        // Task<IActionResult>: trả về 1 HTTP res
        {
            try
            {
                var currentUserId = GetUserId();

                //check thiếu nd, check tồn tại conveer, check quyền
                if (string.IsNullOrWhiteSpace(request.Content))
                    return BadRequest(ApiResponse.Fail("Thiếu nội dung tin nhắn"));

                var conversation = await FindConversationById(conversationId);

                if (conversation == null)
                    return NotFound(ApiResponse.Fail("Conversation Không tồn tại"));

                if (!ConversationHelper.IsParticipant(conversation, currentUserId))
                    return Forbid();

                //tạo object message
                var message = new Message
                {
                    ConversationId = conversationId,
                    SenderId = currentUserId,
                    Content = request.Content.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                //lưu tin nhắn vào data
                await _messageCollection.InsertOneAsync(message);

                // Cập nhật unread trong Conversation
                ConversationHelper.UpdateAfterCreateMessage(conversation, message, currentUserId);

                //update coversation
                await _conversationCollection.ReplaceOneAsync(
                    x => x.Id == conversation.Id,
                    conversation
                );

                //format lại message
                var messageData = await FormatMessage(message, currentUserId);

                //gửi signalR
                await _chatHub.Clients
                    .Group(ChatHub.ConversationGroup(conversationId))
                    .SendAsync("new-message", new
                    {
                        message = messageData,
                        conversation = await FormatConversation(conversation, currentUserId),
                        unreadCounts = conversation.UnreadCounts
                    });

                return StatusCode(201, ApiResponse.Success(
                    "Gửi tin nhắn thành công",
                    messageData
                ));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi gửi tin nhắn");
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        // ÄÃ¡nh dáº¥u tin nháº¯n cuá»‘i trong conversation lÃ  Ä‘Ã£ xem.
        [HttpPatch("{conversationId}/seen")]
        // Đánh dấu tin nhắn cuối là đã xem cho user hiện tại.
        public async Task<IActionResult> MarkAsSeen(string conversationId)
        {
            try
            {
                var currentUserId = GetUserId();

                var conversation = await FindConversationById(conversationId);

                if (conversation == null)
                    return NotFound(ApiResponse.Fail("Conversation Lkhoong tồn tại "));

                if (!ConversationHelper.IsParticipant(conversation, currentUserId))
                    return Forbid();

                // kiểm tra conversation có tin nhắn hay ko 
                if (conversation.LastMessage == null)
                    return Ok(ApiResponse.Success("Không có tin nhắn để đánh dấu đã xem"));

                // kiểm tra người gửi
                if (conversation.LastMessage.SenderId == currentUserId)
                    return Ok(ApiResponse.Success("Người gửi ko cần đánh dấu đã xem"));

                // thêm vào seen by
                if (!conversation.SeenBy.Contains(currentUserId))
                    conversation.SeenBy.Add(currentUserId);

                //reset unread
                conversation.UnreadCounts[currentUserId] = 0;
                conversation.UpdatedAt = DateTime.UtcNow;

                // update data
                await _conversationCollection.ReplaceOneAsync(
                    x => x.Id == conversation.Id,
                    conversation
                );

                //gửi signalR
                await _chatHub.Clients
                    .Group(ChatHub.ConversationGroup(conversationId))
                    .SendAsync("read-message", new
                    {
                        conversation = await FormatConversation(conversation, currentUserId),
                        lastMessage = conversation.LastMessage
                    });

                return Ok(ApiResponse.Success("Đã đánh dấu đã xem", new
                {
                    seenBy = conversation.SeenBy,
                    myUnreadCount = 0
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lá»—i khi Ä‘Ã¡nh dáº¥u Ä‘Ã£ xem");
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }
    }
}
