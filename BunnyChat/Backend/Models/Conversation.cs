using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BunnyChat.Models
{
    //thông tin thành viên
    public class Participant
    {
        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; set; } = string.Empty;

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }

//thông tin nhóm
    public class GroupInfo
    {
        public string? Name { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string? CreatedBy { get; set; }
    }

//thông ton tin nhắn cuối
    public class LastMessage
    {
        public string? Id { get; set; }

        public string? Content { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string? SenderId { get; set; }

        public DateTime? CreatedAt { get; set; }
    }

//thông tin cuộc trò chuyện
    public class Conversation
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        // direct | group
        public string Type { get; set; } = "direct";

        // thành viên tham gia
        public List<Participant> Participants { get; set; } = new();

        // chỉ dùng cho group
        public GroupInfo? Group { get; set; }

        // thời gian tin nhắn cuối
        public DateTime? LastMessageAt { get; set; }

        // danh sách user đã xem tin nhắn cuối
        [BsonRepresentation(BsonType.ObjectId)]
        public List<string> SeenBy { get; set; } = new();

        // cache tin nhắn cuối
        public LastMessage? LastMessage { get; set; }

        // key = userId
        // value = số tin chưa đọc
        public Dictionary<string, int> UnreadCounts { get; set; }
            = new();

        public DateTime CreatedAt { get; set; }
            = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; }
            = DateTime.UtcNow;
    }
}
