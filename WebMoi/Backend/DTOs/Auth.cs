using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using WebMoi.Data;
using WebMoi.Models.Entities;

namespace WebMoi.DTOs
{
    public record SignUpRequest
    {
        // [Required]
        [EmailAddress]
        public string? Email {get; set; }  
        
        // [Required]
        [MinLength(6)]
        public string? PassWord {get; set; }  

         public string? FirstName { get; set; }  

        public string? LastName { get; set; }  

        public string? UserName {get; set; }  

        public string? NickName {get; set; }
    }

    public record LoginRequest
    {
        public string? UserName {get; set; }  

        public string? PassWord {get; set; }  
    
    }

}