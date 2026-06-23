using System.ComponentModel.DataAnnotations;

namespace BunnyChat.DTOs
{
    //DTO data required gửi lên server khi tạo cuộc trò chuyện mới
    public class CreateConversationDTORequest
    {
        [Required]
        public string Type { get; set; } = string.Empty;    //loại trò chuyện: group(chat nhóm) or private (chat riêng 1:1)

        public string? Name { get; set; }

        [Required]
        //danh sách thành viên trong cuộc trò chuyện, dùng chung cho group và pri (tối thiếu 1 member)
        public List<string> MemberIds { get; set; } = new();
    }

    //DTO data required gửi lên server khi gửi 1 tin nhắn 
    public class SendMessageDTORequest
    {
        //id người nhận (thường dùng khi chat 1:1 )
        public string? RecipientId { get; set; }

        public string? ConversationId { get; set; }

        // nội dung tin nhắn
        public string? Content { get; set; }
    }

    // DTO gửi lên server khi thêm thành viên mới vào nhóm chat.
    public class AddGroupMembersDTORequest
    {
        [Required]
        public List<string> MemberIds { get; set; } = new();
    }
}
