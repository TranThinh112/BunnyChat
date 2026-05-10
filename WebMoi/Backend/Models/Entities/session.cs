using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WebMoi.Models.Entities
{
    public class Session
    {
        public  string? AcsessToken { get; set; }

        [BsonElement] 
        public string? Username {get; set;} 
    }
}