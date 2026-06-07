using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using BunnyChat.Service;
using BunnyChat.Models;

namespace BunnyChat.DTOs
{
    public record SignUpDTORequest
    {
         [Required]
        [EmailAddress]
        public string Email {get; set; } = ""; 
        
         [Required]
        [StringLength(100, MinimumLength = 6)]
        public string PassWord {get; set; } = "";  

         public string FirstName { get; set; }  = "";  

        public string LastName { get; set; }  = "";  

        public string UserName {get; set; }  = "";  

        public string NickName {get; set; } = "";  
    }

    public class LoginDTORequest
    {
        [Required]
        public string UserName {get; set; } = "";

        [Required]
        public string PassWord {get; set; }  = "";
    
    }

}