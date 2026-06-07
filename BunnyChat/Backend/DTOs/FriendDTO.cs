using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using BunnyChat.Service;
using BunnyChat.Models;

namespace BunnyChat.DTOs
{
	public class FriendDTORequest()
    {
		// [Required]
		// public string FromId {get; set;} = null!;

		[Required]
		public string ReceiveId {get; set;} = null!;

		public string?  Message { get; set; }
	}
}
