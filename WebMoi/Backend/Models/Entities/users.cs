using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WebMoi.Models.Entities
{
    public class User
    {
        // {get; set; }: cho phep doc  va ghi

        [BsonId]
        [BsonElement("_id"), BsonRepresentation(BsonType.ObjectId)] 
        public  string? Id {get; set; }

        public  string? FirstName { get; set; }
        public  string? LastName { get; set; }

        public  string? Email { get; set; } //="";
        
        public  string? HashPassword { get; set; } 

        [BsonElement] 
        public string? Username {get; set;} 

        public string? AvatarUrl { get; set; } 
        public string? Bio {get; set; }
        public string? Phone {get; set; }

        public string? Nickname {get; set;} 

        [BsonIgnore]
        public string DisplayName =>
            !string.IsNullOrWhiteSpace(Nickname)
                ? $"{FirstName} {LastName} ({Nickname})"
                : $"{FirstName} {LastName}";

        public DateTime CreatedAt {get; set; }
        public DateTime UpdatedAt {get; set; }
    

    }
}