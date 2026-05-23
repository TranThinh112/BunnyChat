using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using WebMoi.Data;
using WebMoi.Models.Entities;

namespace WebMoi.DTOs
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
