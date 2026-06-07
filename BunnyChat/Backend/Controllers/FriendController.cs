using System.Security.Cryptography.X509Certificates;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using BunnyChat.Service;
using BunnyChat.DTOs;
using BunnyChat.Models;

namespace BunnyChat.Controllers
{
    [Authorize]
    [ApiController]
    [Route("/apifriend/")]
    public class FriendController : ControllerBase
    {
        private readonly IMongoCollection<Friend> _friendCollection;

        
        public FriendController(MongoDbService mongoDbService) 
        {
            _friendCollection = mongoDbService.Database.GetCollection<Friend>("friends");
        }

        [HttpGet]
        public async Task<IEnumerable<Friend>> Get() //IEnumerable: danh sach cac user, Task: async, ActionResult: tra ve 200, 404, 500, có thể là map or list
        {   
            return await _friendCollection.Find(FilterDefinition<Friend>.Empty).ToListAsync();
        }

    }

}
