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
                .Where(x => !string.IsNullOrWhiteSpace(x)) //bỏ khoảng trắng
                .Distinct() //bỏ trùng
                .ToList();
        }

        // Cập nhật cache tin nhắn cuối và số tin chưa đọc sau khi tạo message mới.
        // đếm số tin nhắn chưa đọc của từng người
        // Khi UserA gửi tin nhắn, hệ thống đánh dấu UserA là đã xem,
        // tăng số tin chưa đọc cho các thành viên khác lên 1, giữ UserA là 0 tin chưa đọc, rồi cập nhật thời gian thay đổi của conversation.
        public static void UpdateAfterCreateMessage(
            Conversation conversation,
            Message message,
            string senderId)
        {
            //Cập nhật thời gian tin nhắn cuối để danh sách conversation đc sort lại
            conversation.LastMessageAt = message.CreatedAt;

            //Cache tin nhắn cuối. Lưu nhanh: 
                // Tin nhắn cuối để không cần query collection Message mỗi lần load danh sách chat.
            conversation.LastMessage = new LastMessage
            {
                Id = message.Id,
                Content = message.Content,
                SenderId = senderId,
                CreatedAt = message.CreatedAt
            };

            //chạy sau khi gửi tin nhắn mới
                //Người gửi chắc chắn đã xem tin nhắn.
                //ví dụ nhóm có 3 user: userA, userB, userC
                //senderId = "UserA" => Tin nhắn mới này hiện tại chỉ có người gửi là đã xem.
            conversation.SeenBy = new List<string> { senderId }; 

            // cập nhật số lượng người chưa đọc tin nhắn
                // Vòng lặp qua từng thành viên
            foreach (var participantId in GetParticipantIds(conversation))
            {
                //Lấy số tin chưa đọc cũ
                //Ví dụ ban đầu:
                    // "unreadCounts": {
                        //   "UserA": 0,
                        //   "UserB": 2,
                        //   "UserC": 5
                    // }

                    // Nó kiểm tra:
                        // UserA đang có 0 tin chưa đọc.
                        // UserB đang có 2 tin chưa đọc.
                        // UserC đang có 5 tin chưa đọc.

                    // Nếu user chưa có trong UnreadCounts thì lấy mặc định là 0.
                var oldCount = conversation.UnreadCounts.ContainsKey(participantId)
                    ? conversation.UnreadCounts[participantId]
                    : 0;

                    //Cập nhật số tin chưa đọc. Nếu là sender thì set về 0
                    //nếu là người khác thì oldCount +1
                conversation.UnreadCounts[participantId] =
                    participantId == senderId ? 0 : oldCount + 1;
            }
                //Cập nhật thời gian conversation vừa có thay đổi. Sắp xếp danh sách chat
                // Cuộc trò chuyện mới cập nhật sẽ nằm trên cùng
            conversation.UpdatedAt = DateTime.UtcNow;
        }
    }
}
