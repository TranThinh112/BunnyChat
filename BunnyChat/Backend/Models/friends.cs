using System;
using System.Security.Cryptography.X509Certificates;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BunnyChat.Models.Entities 
{


//cac trang thai cua loi moi ket ban
	public enum FriendStatus
	{
		Pending,
		Accepted,
		Rejected,
		Blocked
	}
	public class Friend
	{
		[BsonId]
		[BsonElement("_id"), BsonRepresentation(BsonType.ObjectId)] 

		public string _Id {get; set;} = null!;
		public string SenderId {get; set;} = null!;
		public string ReceiveId {get; set;} = null!;
		public string? Message {get; set;}
		public FriendStatus Status {get; set;} 
		
        public DateTime CreatedAt {get; set; }
        public DateTime UpdatedAt {get; set; }
    
	}
}
