using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BunnyChat.Models
{
    [BsonIgnoreExtraElements]
    public class Session
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}