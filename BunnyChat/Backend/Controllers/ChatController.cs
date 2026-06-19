using BunnyChat.DTOs;
using BunnyChat.Helper;
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
    [Route("/api/chat")]
    public class ChatController : ControllerBase
    {
        private readonly IMongoCollection<Conversation> _conversationCollection;
        private readonly IMongoCollection<Message> _messageCollection;
        private readonly IMongoCollection<User> _userCollection;

        public ChatController(MongoDbService mongoDbService)
        {
            _conversationCollection = mongoDbService.Database.GetCollection<Conversation>("conversations");
            _messageCollection = mongoDbService.Database.GetCollection<Message>("messages");
            _userCollection = mongoDbService.Database.GetCollection<User>("users");
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

        // Format conversation để frontend render card nhóm, bạn bè và thông tin nhóm.
        private async Task<object> FormatConversation(Conversation conversation, string currentUserId)
        {
            var participantIds = ConversationHelper.GetParticipantIds(conversation);
            var users = await _userCollection
                .Find(x => participantIds.Contains(x.Id!))
                .ToListAsync();

            var members = users.Select(user => new
            {
                id = user.Id,
                username = user.Username,
                displayname = user.DisplayName,
                avatarUrl = user.AvatarUrl
            }).ToList();

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
                username = directUser?.Username,
                memberCount = participantIds.Count,
                members,
                createdAt = conversation.CreatedAt,
                updatedAt = conversation.UpdatedAt,
                lastMessage = conversation.LastMessage?.Content,
                lastMessageAt = conversation.LastMessageAt,
                unreadCount = conversation.UnreadCounts.ContainsKey(currentUserId)
                    ? conversation.UnreadCounts[currentUserId]
                    : 0
            };
        }

        // Tạo direct chat hoặc group chat mới.
        [HttpPost]
        public async Task<IActionResult> CreateConversation(CreateConversationDTORequest request)
        {
            try
            {
                var currentUserId = GetUserId();

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return Unauthorized(ApiResponse.Fail("Token không hợp lệ"));

                if (request.MemberIds == null || !request.MemberIds.Any())
                    return BadRequest(ApiResponse.Fail("Danh sách thành viên không được trống"));

                var type = request.Type.Trim().ToLower();
                var memberIds = request.MemberIds
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();

                if (type != "direct" && type != "group")
                    return BadRequest(ApiResponse.Fail("Loại conversation không hợp lệ"));

                if (type == "group" && string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(ApiResponse.Fail("Tên nhóm là bắt buộc"));

                if (type == "direct")
                {
                    if (memberIds.Count != 1 || memberIds.First() == currentUserId)
                        return BadRequest(ApiResponse.Fail("Direct chat cần đúng một người nhận khác user hiện tại"));

                    var participantId = memberIds.First();
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

                var allMemberIds = new List<string> { currentUserId }
                    .Concat(memberIds)
                    .Distinct()
                    .ToList();

                foreach (var memberId in allMemberIds)
                {
                    var user = await FindUserById(memberId);
                    if (user == null)
                        return NotFound(ApiResponse.Fail($"Không tìm thấy user {memberId}"));
                }

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

                conversation.UnreadCounts = allMemberIds.ToDictionary(x => x, _ => 0);

                await _conversationCollection.InsertOneAsync(conversation);

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

        // Lấy toàn bộ conversation của user hiện tại.
        [HttpGet]
        public async Task<IActionResult> GetConversations()
        {
            try
            {
                var currentUserId = GetUserId();

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return Unauthorized(ApiResponse.Fail("Token không hợp lệ"));

                var conversations = await _conversationCollection
                    .Find(x => x.Participants.Any(p => p.UserId == currentUserId))
                    .SortByDescending(x => x.LastMessageAt)
                    .ThenByDescending(x => x.UpdatedAt)
                    .ToListAsync();

                var data = new List<object>();

                foreach (var conversation in conversations)
                {
                    data.Add(await FormatConversation(conversation, currentUserId));
                }

                return Ok(ApiResponse.Success("Lấy danh sách conversation thành công", data));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi lấy conversations");
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        // Lấy danh sách group chat của user hiện tại, dùng cho sidebar frontend.
        [HttpGet("groups")]
        public async Task<IActionResult> GetGroups()
        {
            try
            {
                var currentUserId = GetUserId();

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return Unauthorized(ApiResponse.Fail("Token không hợp lệ"));

                var groups = await _conversationCollection
                    .Find(x => x.Type == "group" && x.Participants.Any(p => p.UserId == currentUserId))
                    .SortByDescending(x => x.LastMessageAt)
                    .ThenByDescending(x => x.UpdatedAt)
                    .ToListAsync();

                var data = new List<object>();

                foreach (var group in groups)
                {
                    data.Add(await FormatConversation(group, currentUserId));
                }

                return Ok(ApiResponse.Success("Lấy danh sách nhóm thành công", data));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi lấy danh sách nhóm");
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        // Lấy tin nhắn theo conversation, hỗ trợ phân trang bằng cursor createdAt.
        [HttpGet("{conversationId}/messages")]
        public async Task<IActionResult> GetMessages(string conversationId, int limit = 50, DateTime? cursor = null)
        {
            try
            {
                var currentUserId = GetUserId();

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return Unauthorized(ApiResponse.Fail("Token không hợp lệ"));

                var conversation = await FindConversationById(conversationId);

                if (conversation == null)
                    return NotFound(ApiResponse.Fail("Conversation không tồn tại"));

                if (!ConversationHelper.IsParticipant(conversation, currentUserId))
                    return Forbid();

                var filter = Builders<Message>.Filter.Eq(x => x.ConversationId, conversationId);

                if (cursor.HasValue)
                {
                    filter &= Builders<Message>.Filter.Lt(x => x.CreatedAt, cursor.Value);
                }

                var pageSize = Math.Clamp(limit, 1, 100);
                var messages = await _messageCollection
                    .Find(filter)
                    .SortByDescending(x => x.CreatedAt)
                    .Limit(pageSize + 1)
                    .ToListAsync();

                DateTime? nextCursor = null;

                if (messages.Count > pageSize)
                {
                    nextCursor = messages.Last().CreatedAt;
                    messages.RemoveAt(messages.Count - 1);
                }

                var data = messages
                    .OrderBy(x => x.CreatedAt)
                    .Select(x => new
                    {
                        id = x.Id,
                        conversationId = x.ConversationId,
                        senderId = x.SenderId,
                        content = x.Content,
                        imgUrl = x.ImgUrl,
                        isMine = x.SenderId == currentUserId,
                        createdAt = x.CreatedAt,
                        updatedAt = x.UpdatedAt
                    });

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

        // Gửi tin nhắn vào conversation đã tồn tại.
        [HttpPost("{conversationId}/messages")]
        public async Task<IActionResult> SendMessage(string conversationId, SendMessageDTORequest request)
        {
            try
            {
                var currentUserId = GetUserId();

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return Unauthorized(ApiResponse.Fail("Token không hợp lệ"));

                if (string.IsNullOrWhiteSpace(request.Content))
                    return BadRequest(ApiResponse.Fail("Thiếu nội dung tin nhắn"));

                var conversation = await FindConversationById(conversationId);

                if (conversation == null)
                    return NotFound(ApiResponse.Fail("Conversation không tồn tại"));

                if (!ConversationHelper.IsParticipant(conversation, currentUserId))
                    return Forbid();

                var message = new Message
                {
                    ConversationId = conversationId,
                    SenderId = currentUserId,
                    Content = request.Content.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _messageCollection.InsertOneAsync(message);

                ConversationHelper.UpdateAfterCreateMessage(conversation, message, currentUserId);

                await _conversationCollection.ReplaceOneAsync(
                    x => x.Id == conversation.Id,
                    conversation
                );

                return StatusCode(201, ApiResponse.Success(
                    "Gửi tin nhắn thành công",
                    new
                    {
                        id = message.Id,
                        conversationId = message.ConversationId,
                        senderId = message.SenderId,
                        content = message.Content,
                        imgUrl = message.ImgUrl,
                        isMine = true,
                        createdAt = message.CreatedAt,
                        updatedAt = message.UpdatedAt
                    }
                ));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi gửi tin nhắn");
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        // Đánh dấu tin nhắn cuối trong conversation là đã xem.
        [HttpPatch("{conversationId}/seen")]
        public async Task<IActionResult> MarkAsSeen(string conversationId)
        {
            try
            {
                var currentUserId = GetUserId();

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return Unauthorized(ApiResponse.Fail("Token không hợp lệ"));

                var conversation = await FindConversationById(conversationId);

                if (conversation == null)
                    return NotFound(ApiResponse.Fail("Conversation không tồn tại"));

                if (!ConversationHelper.IsParticipant(conversation, currentUserId))
                    return Forbid();

                if (conversation.LastMessage == null)
                    return Ok(ApiResponse.Success("Không có tin nhắn để đánh dấu đã xem"));

                if (conversation.LastMessage.SenderId == currentUserId)
                    return Ok(ApiResponse.Success("Người gửi không cần đánh dấu đã xem"));

                if (!conversation.SeenBy.Contains(currentUserId))
                    conversation.SeenBy.Add(currentUserId);

                conversation.UnreadCounts[currentUserId] = 0;
                conversation.UpdatedAt = DateTime.UtcNow;

                await _conversationCollection.ReplaceOneAsync(
                    x => x.Id == conversation.Id,
                    conversation
                );

                return Ok(ApiResponse.Success("Đã đánh dấu đã xem", new
                {
                    seenBy = conversation.SeenBy,
                    myUnreadCount = 0
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi đánh dấu đã xem");
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }
    }
}
