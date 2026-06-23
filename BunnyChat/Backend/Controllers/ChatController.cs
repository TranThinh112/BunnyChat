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

        public ChatController(MongoDbService mongoDbService, IHubContext<ChatHub> chatHub)
        {
            _conversationCollection = mongoDbService.Database.GetCollection<Conversation>("conversations");
            _messageCollection = mongoDbService.Database.GetCollection<Message>("messages");
            _userCollection = mongoDbService.Database.GetCollection<User>("users");
            _chatHub = chatHub;
        }

        // Láº¥y userId tá»« claim trong access token.
        private string? GetUserId()
        {
            return User.FindFirst("userId")?.Value;
        }

        // TÃ¬m conversation theo id.
        private async Task<Conversation?> FindConversationById(string conversationId)
        {
            return await _conversationCollection
                .Find(x => x.Id == conversationId)
                .FirstOrDefaultAsync();
        }

        // TÃ¬m user theo id Ä‘á»ƒ kiá»ƒm tra thÃ nh viÃªn cÃ³ tá»“n táº¡i khÃ´ng.
        private async Task<User?> FindUserById(string userId)
        {
            return await _userCollection
                .Find(x => x.Id == userId)
                .FirstOrDefaultAsync();
        }

        // Format conversation Ä‘á»ƒ frontend render card nhÃ³m, báº¡n bÃ¨ vÃ  thÃ´ng tin nhÃ³m.
        private async Task<object> FormatConversation(Conversation conversation, string currentUserId)
        {
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
                lastMessage = conversation.LastMessage?.Content,
                lastMessageAt = conversation.LastMessageAt,
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

        // Táº¡o direct chat hoáº·c group chat má»›i.
        [HttpPost]
        public async Task<IActionResult> CreateConversation(CreateConversationDTORequest request)
        {
            try
            {
                var currentUserId = GetUserId();

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return Unauthorized(ApiResponse.Fail("Token khÃ´ng há»£p lá»‡"));

                if (request.MemberIds == null || !request.MemberIds.Any())
                    return BadRequest(ApiResponse.Fail("Danh sÃ¡ch thÃ nh viÃªn khÃ´ng Ä‘Æ°á»£c trá»‘ng"));

                var type = request.Type.Trim().ToLower();
                var memberIds = request.MemberIds
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();

                if (type != "direct" && type != "group")
                    return BadRequest(ApiResponse.Fail("Loáº¡i conversation khÃ´ng há»£p lá»‡"));

                if (type == "group" && string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(ApiResponse.Fail("TÃªn nhÃ³m lÃ  báº¯t buá»™c"));

                if (type == "direct")
                {
                    if (memberIds.Count != 1 || memberIds.First() == currentUserId)
                        return BadRequest(ApiResponse.Fail("Direct chat cáº§n Ä‘Ãºng má»™t ngÆ°á»i nháº­n khÃ¡c user hiá»‡n táº¡i"));

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
                            "Conversation Ä‘Ã£ tá»“n táº¡i",
                            await FormatConversation(oldConversation, currentUserId)
                        ));
                    }
                }

                if (type == "group" && memberIds.Count < 1)
                    return BadRequest(ApiResponse.Fail("NhÃ³m cáº§n Ã­t nháº¥t má»™t thÃ nh viÃªn ngoÃ i user hiá»‡n táº¡i"));

                var allMemberIds = new List<string> { currentUserId }
                    .Concat(memberIds)
                    .Distinct()
                    .ToList();

                foreach (var memberId in allMemberIds)
                {
                    var user = await FindUserById(memberId);
                    if (user == null)
                        return NotFound(ApiResponse.Fail($"KhÃ´ng tÃ¬m tháº¥y user {memberId}"));
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

                foreach (var memberId in allMemberIds)
                {
                    var memberConversation = await FormatConversation(conversation, memberId);

                    await _chatHub.Clients
                        .Group(ChatHub.UserGroup(memberId))
                        .SendAsync("new-conversation", memberConversation);
                }

                return StatusCode(201, ApiResponse.Success(
                    "Táº¡o conversation thÃ nh cÃ´ng",
                    await FormatConversation(conversation, currentUserId)
                ));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lá»—i khi táº¡o conversation");
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        // Láº¥y toÃ n bá»™ conversation cá»§a user hiá»‡n táº¡i.
        [HttpGet]
        public async Task<IActionResult> GetConversations()
        {
            try
            {
                var currentUserId = GetUserId();

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return Unauthorized(ApiResponse.Fail("Token khÃ´ng há»£p lá»‡"));

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

                return Ok(ApiResponse.Success("Láº¥y danh sÃ¡ch conversation thÃ nh cÃ´ng", data));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lá»—i khi láº¥y conversations");
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        // Láº¥y danh sÃ¡ch group chat cá»§a user hiá»‡n táº¡i, dÃ¹ng cho sidebar frontend.
        [HttpGet("groups")]
        public async Task<IActionResult> GetGroups()
        {
            try
            {
                var currentUserId = GetUserId();

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return Unauthorized(ApiResponse.Fail("Token khÃ´ng há»£p lá»‡"));

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

                return Ok(ApiResponse.Success("Láº¥y danh sÃ¡ch nhÃ³m thÃ nh cÃ´ng", data));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lá»—i khi láº¥y danh sÃ¡ch nhÃ³m");
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        // Thêm thành viên mới vào nhóm chat. Nếu user đã ở trong nhóm thì không thêm trùng.
        [HttpPost("{conversationId}/members")]
        public async Task<IActionResult> AddGroupMembers(string conversationId, AddGroupMembersDTORequest request)
        {
            try
            {
                var currentUserId = GetUserId();

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return Unauthorized(ApiResponse.Fail("Token khÃ´ng há»£p lá»‡"));

                if (request.MemberIds == null || !request.MemberIds.Any())
                    return BadRequest(ApiResponse.Fail("Danh sÃ¡ch thÃ nh viÃªn khÃ´ng Ä‘Æ°á»£c trá»‘ng"));

                var conversation = await FindConversationById(conversationId);

                if (conversation == null)
                    return NotFound(ApiResponse.Fail("Conversation khÃ´ng tá»“n táº¡i"));

                if (conversation.Type != "group")
                    return BadRequest(ApiResponse.Fail("Chá»‰ cÃ³ thá»ƒ thÃªm thÃ nh viÃªn vÃ o nhÃ³m chat"));

                if (!ConversationHelper.IsParticipant(conversation, currentUserId))
                    return Forbid();

                var oldMemberIds = ConversationHelper.GetParticipantIds(conversation);
                var newMemberIds = request.MemberIds
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .Where(x => !oldMemberIds.Contains(x))
                    .ToList();

                if (!newMemberIds.Any())
                    return Conflict(ApiResponse.Fail("CÃ¡c user nÃ y Ä‘Ã£ thuá»™c nhÃ³m chat"));

                foreach (var memberId in newMemberIds)
                {
                    var user = await FindUserById(memberId);
                    if (user == null)
                        return NotFound(ApiResponse.Fail($"KhÃ´ng tÃ¬m tháº¥y user {memberId}"));
                }

                foreach (var memberId in newMemberIds)
                {
                    conversation.Participants.Add(new Participant
                    {
                        UserId = memberId,
                        JoinedAt = DateTime.UtcNow
                    });

                    if (!conversation.UnreadCounts.ContainsKey(memberId))
                        conversation.UnreadCounts[memberId] = 0;
                }

                conversation.UpdatedAt = DateTime.UtcNow;

                await _conversationCollection.ReplaceOneAsync(
                    x => x.Id == conversation.Id,
                    conversation
                );

                var allMemberIds = ConversationHelper.GetParticipantIds(conversation);
                foreach (var memberId in allMemberIds)
                {
                    await _chatHub.Clients
                        .Group(ChatHub.UserGroup(memberId))
                        .SendAsync("new-conversation", await FormatConversation(conversation, memberId));
                }

                return Ok(ApiResponse.Success(
                    "ThÃªm thÃ nh viÃªn vÃ o nhÃ³m thÃ nh cÃ´ng",
                    await FormatConversation(conversation, currentUserId)
                ));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lá»—i khi thÃªm thÃ nh viÃªn vÃ o nhÃ³m");
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
                    return Unauthorized(ApiResponse.Fail("Token khÃ´ng há»£p lá»‡"));

                var conversation = await FindConversationById(conversationId);

                if (conversation == null)
                    return NotFound(ApiResponse.Fail("Conversation khÃ´ng tá»“n táº¡i"));

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

                var data = new List<object>();
                foreach (var message in messages.OrderBy(x => x.CreatedAt))
                {
                    data.Add(await FormatMessage(message, currentUserId));
                }

                return Ok(ApiResponse.Success(
                    "Láº¥y tin nháº¯n thÃ nh cÃ´ng",
                    new
                    {
                        messages = data,
                        nextCursor
                    }
                ));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lá»—i khi láº¥y tin nháº¯n");
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        // Gá»­i tin nháº¯n vÃ o conversation Ä‘Ã£ tá»“n táº¡i.
        [HttpPost("{conversationId}/messages")]
        public async Task<IActionResult> SendMessage(string conversationId, SendMessageDTORequest request)
        {
            try
            {
                var currentUserId = GetUserId();

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return Unauthorized(ApiResponse.Fail("Token khÃ´ng há»£p lá»‡"));

                if (string.IsNullOrWhiteSpace(request.Content))
                    return BadRequest(ApiResponse.Fail("Thiáº¿u ná»™i dung tin nháº¯n"));

                var conversation = await FindConversationById(conversationId);

                if (conversation == null)
                    return NotFound(ApiResponse.Fail("Conversation khÃ´ng tá»“n táº¡i"));

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

                var messageData = await FormatMessage(message, currentUserId);

                await _chatHub.Clients
                    .Group(ChatHub.ConversationGroup(conversationId))
                    .SendAsync("new-message", new
                    {
                        message = messageData,
                        conversation = await FormatConversation(conversation, currentUserId),
                        unreadCounts = conversation.UnreadCounts
                    });

                return StatusCode(201, ApiResponse.Success(
                    "Gá»­i tin nháº¯n thÃ nh cÃ´ng",
                    messageData
                ));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lá»—i khi gá»­i tin nháº¯n");
                return StatusCode(500, ApiResponse.Fail(ex.Message));
            }
        }

        // ÄÃ¡nh dáº¥u tin nháº¯n cuá»‘i trong conversation lÃ  Ä‘Ã£ xem.
        [HttpPatch("{conversationId}/seen")]
        public async Task<IActionResult> MarkAsSeen(string conversationId)
        {
            try
            {
                var currentUserId = GetUserId();

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return Unauthorized(ApiResponse.Fail("Token khÃ´ng há»£p lá»‡"));

                var conversation = await FindConversationById(conversationId);

                if (conversation == null)
                    return NotFound(ApiResponse.Fail("Conversation khÃ´ng tá»“n táº¡i"));

                if (!ConversationHelper.IsParticipant(conversation, currentUserId))
                    return Forbid();

                if (conversation.LastMessage == null)
                    return Ok(ApiResponse.Success("KhÃ´ng cÃ³ tin nháº¯n Ä‘á»ƒ Ä‘Ã¡nh dáº¥u Ä‘Ã£ xem"));

                if (conversation.LastMessage.SenderId == currentUserId)
                    return Ok(ApiResponse.Success("NgÆ°á»i gá»­i khÃ´ng cáº§n Ä‘Ã¡nh dáº¥u Ä‘Ã£ xem"));

                if (!conversation.SeenBy.Contains(currentUserId))
                    conversation.SeenBy.Add(currentUserId);

                conversation.UnreadCounts[currentUserId] = 0;
                conversation.UpdatedAt = DateTime.UtcNow;

                await _conversationCollection.ReplaceOneAsync(
                    x => x.Id == conversation.Id,
                    conversation
                );

                await _chatHub.Clients
                    .Group(ChatHub.ConversationGroup(conversationId))
                    .SendAsync("read-message", new
                    {
                        conversation = await FormatConversation(conversation, currentUserId),
                        lastMessage = conversation.LastMessage
                    });

                return Ok(ApiResponse.Success("ÄÃ£ Ä‘Ã¡nh dáº¥u Ä‘Ã£ xem", new
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
