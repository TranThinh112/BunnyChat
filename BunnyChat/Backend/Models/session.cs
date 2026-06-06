using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BunnyChat.Models.Entities
{
   public class Session
    {
        public string UserId { get; set; } = null!;
        public string Username { get; set; } = null!;

        public string RefreshToken { get; set; } = null!;

        public DateTime ExpiresAt { get; set; }
    }
}