using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using WebMoi.Data;
using WebMoi.DTOs;
using WebMoi.Models.Entities;

namespace WebMoi.Controllers


{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        
        private readonly IMongoCollection<User> _usersCollection;

        public AuthController(MongoDbService mongoDbService)
        {
            _usersCollection = mongoDbService.Database.GetCollection<User>("users");
        }
       
        [HttpPost("signup")]
        public async Task<ActionResult> Create(Signup request) 
        {
             var user = new User
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Username = $"{request.FirstName} {request.LastName}",
                Email = request.Email,
                HashPassword = request.PassWord, 
                CreatedAt = DateTime.UtcNow,
            };
            
            await _usersCollection.InsertOneAsync(user);
            
            return Ok(new
            {   
                success = true,
                status = 200,
                user = new
                {
                    id = user.Id,
                    username = user.Username
                }
            });
        }
    }
}
