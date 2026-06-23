using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using BunnyChat.Service;
using BunnyChat.Models;

namespace BunnyChat.DTOs
{
    public class UserInformationDTORequest
    {
        public string? Bio { get; set; }

        //regex cháº¥p nháº­n sÄ‘t cÃ³ + mÃ£ vÃ¹ng
        [RegularExpression(@"^(0\d{9}|84\d{9}|\+84\d{9})$",
       ErrorMessage = "Số điện thoại phải có dạng 0xxxxxxxxx hoặc +84xxxxxxxxx")]
        public string? Phone { get; set; }

        public string? Nickname { get; set; }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        public string? AvatarUrl { get; set; }
    }

}
