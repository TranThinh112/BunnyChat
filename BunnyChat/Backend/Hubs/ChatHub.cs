using BunnyChat.Helper;
using BunnyChat.Models;
using BunnyChat.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;

namespace BunnyChat.Backend.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IMongoCollection<Conversation> _conversationCollection;

        public ChatHub(MongoDbService mongoDbService)
        {
            _conversationCollection = mongoDbService.Database.GetCollection<Conversation>("conversations");
        }

        // Lấy userId từ access token đã được JWT middleware xác thực.
        private string? GetUserId()
        {
            return Context.User?.FindFirst("userId")?.Value;
        }

        // Tạo tên group riêng cho từng user để gửi sự kiện cá nhân như conversation mới.
        public static string UserGroup(string userId)
        {
            return $"user:{userId}";
        }

        // Tạo tên group riêng cho từng conversation để gửi realtime message.
        public static string ConversationGroup(string conversationId)
        {
            return $"conversation:{conversationId}";
        }

        // Khi client kết nối, đưa connection vào group riêng của user.
        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();

            if (!string.IsNullOrWhiteSpace(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
            }

            await base.OnConnectedAsync();
        }

        // Client gọi hàm này sau khi tải danh sách chat để nhận realtime cho conversation đó.
        public async Task JoinConversation(string conversationId)
        {
            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(conversationId))
                return;

            var conversation = await _conversationCollection
                .Find(x => x.Id == conversationId)
                .FirstOrDefaultAsync();

            if (conversation == null || !ConversationHelper.IsParticipant(conversation, userId))
                return;

            await Groups.AddToGroupAsync(Context.ConnectionId, ConversationGroup(conversationId));
        }

        // Client gọi hàm này khi không cần nghe realtime của một conversation nữa.
        public async Task LeaveConversation(string conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                return;

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, ConversationGroup(conversationId));
        }
    }
}
