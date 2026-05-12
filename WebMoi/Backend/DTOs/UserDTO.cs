using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using WebMoi.Data;
using WebMoi.Models.Entities;

namespace WebMoi.DTOs
{
    public class UserInformation
    {   
         public string? Bio { get; set; }

        [RegularExpression(@"^$|^\d{10,15}$",
            ErrorMessage = "Phone must be 10-15 digits")]
        public string? Phone { get; set; }

        public string? Nickname { get; set; }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        public string? AvatarUrl { get; set; }
    }

}