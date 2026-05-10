using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using WebMoi.Data;
using WebMoi.Models.Entities;

namespace WebMoi.DTOs
{
    public class SignUpRequest
    {
        // [Required]
        [EmailAddress]
        public string? Email {get; set; }  
        
        // [Required]
        [MinLength(6)]
        public string? PassWord {get; set; }  

        // [Required]
         public string? FirstName { get; set; }  

        // [Required]
        public string? LastName { get; set; }  

        // [Required]
        public string? UserName {get; set; }  

        public string? NickName {get; set; }
    }

    public class LoginRequest
    {
        public string? UserName {get; set; }  

        public string? PassWord {get; set; }  
    
    }
}