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

        // [Required]
         public string? FirstName { get; set; }  

        // [Required]
        public string? LastName { get; set; }  

        // [Required]
        public string? UserName {get; set; }  

        public string? NickName {get; set; }
    }

    // public record LoginRequest
    // {
    //     public string? UserName {get; set; }  

    //     public string? PassWord {get; set; }  
    
    // }
    // public record LoginRequest(string Username, string Password);

    public record TokenRequest (string AccessToken, string RefreshToken);
    public record TokenResponse (string AccessToken, string RefreshToken);
    public record AssignRole (string Username, string RoleName);



}