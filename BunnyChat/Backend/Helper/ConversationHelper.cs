using BunnyChat.Models;

namespace BunnyChat.Helper
{
    public static class ConversationHelper
    {
        // Kiểm tra user hiện tại có nằm trong danh sách thành viên conversation hay không.
        public static bool IsParticipant(Conversation conversation, string userId)
        {
            return conversation.Participants.Any(x => x.UserId == userId);
        }

        // Lấy danh sách userId của các thành viên trong conversation.
        public static List<string> GetParticipantIds(Conversation conversation)
        {
            return conversation.Participants
                .Select(x => x.UserId) //lấy riêng userId
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();
        }

        // Cập nhật cache tin nhắn cuối và số tin chưa đọc sau khi tạo message mới.
        public static void UpdateAfterCreateMessage(
            Conversation conversation,
            Message message,
            string senderId)
        {
            conversation.LastMessageAt = message.CreatedAt;
            conversation.LastMessage = new LastMessage
            {
                Id = message.Id,
                Content = message.Content,
                SenderId = senderId,
                CreatedAt = message.CreatedAt
            };

            conversation.SeenBy = new List<string> { senderId };

            foreach (var participantId in GetParticipantIds(conversation))
            {
                var oldCount = conversation.UnreadCounts.ContainsKey(participantId)
                    ? conversation.UnreadCounts[participantId]
                    : 0;

                conversation.UnreadCounts[participantId] =
                    participantId == senderId ? 0 : oldCount + 1;
            }

            conversation.UpdatedAt = DateTime.UtcNow;
        }
    }
}
