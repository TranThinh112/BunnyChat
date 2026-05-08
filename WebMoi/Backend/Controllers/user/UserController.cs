using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using WebMoi.Data;
using WebMoi.DTOs;
using WebMoi.Models.Entities;

namespace WebMoi.Controllers

{
    [Route("users/")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IMongoCollection<User> _usersCollection;

        public UserController(MongoDbService mongoDbService) 
        {
            _usersCollection = mongoDbService.Database.GetCollection<User>("users");
        }
        
        //API lay toan bo user
        [HttpGet]
        public async Task<IEnumerable<User>> Get() //IEnumerable: danh sach cac user, Task: async, ActionResult: tra ve 200, 404, 500, có thể là map or list
        {   
            
            return await _usersCollection.Find(FilterDefinition<User>.Empty).ToListAsync();
        }

        //API serach
        [HttpGet("search")]
        public async Task<IActionResult> Search(string? username, string? id, string? email)
        {
            // thiếu query
            if (
                string.IsNullOrWhiteSpace(username) &&
                string.IsNullOrWhiteSpace(id) &&
                string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "username, id or email is required"
                });
            }
        var filters = new List<FilterDefinition<User>>();

            // search id, username, email
            if (!string.IsNullOrWhiteSpace(id))
            {
                if (!ObjectId.TryParse(id, out var objectId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid user id"
                    });
                }

                    filters.Add(
                        Builders<User>.Filter.Eq("_id", objectId)
                    );
            }

            // search username
            if (!string.IsNullOrWhiteSpace(username))
            {
                filters.Add(
                    Builders<User>.Filter.Regex(
                        x => x.Username,
                        new BsonRegularExpression(username, "i")
                    )
                );
            }

            // search email
            if (!string.IsNullOrWhiteSpace(email))
            {
                filters.Add(
                    Builders<User>.Filter.Eq(x => x.Email, email)
                );
            }

            var finalFilter = Builders<User>.Filter.Or(filters);


            var user = await _usersCollection
                .Find(finalFilter)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "User not found"
                });
            }

            return Ok(new
            {
                success = true,
                message = "User found",
                data = user
            });
        }
       
        //upload Avatar
        // [HttpPatch("uploadAvatar/{id}")]
        //  public async Task<ActionResult> UploadAvatar(string id ,UserAvatar request) 
        // {
        //      var update = Builders<User>.Update
        //         .Set(x => x.AvatarUrl, request.AvatarUrl)
        //         .Set(x => x.UpdatedAt, DateTime.UtcNow);

        //     var result = await _usersCollection.UpdateOneAsync(
        //         x => x.Id == id,
        //         update
        //     );

        //     if (result.MatchedCount == 0)
        //     {       
        //         return NotFound(new
        //         {
        //             success = false,
        //             message = "User not found"
        //         });
        //     }
        //     return Ok(new
        //     {
        //         success = true,
        //         message = "Avata updated"
        //     });
        // }
        
        
        //API update Thong tIn
        [HttpPatch("updateInformation/{id}")]
        public async Task<ActionResult> UpdateInformation ( string id, UserInformation request)
        {
             var user = await _usersCollection
                .Find(x => x.Id == id)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "User not found"
                });
            }
            var firstName = request.FirstName ?? user.FirstName;
            var lastName = request.LastName ?? user.LastName;
            var nickname = request.Nickname ?? user.Nickname;
            var bio = request.Bio ?? user.Bio;
            var phone = request.Phone ?? user.Phone;
            var avatar = request.AvatarUrl ?? user.AvatarUrl;

            var username = $"{firstName} {lastName}";

           var update = Builders<User>.Update
                .Set(x => x.FirstName, firstName)
                .Set(x => x.LastName, lastName)
                .Set(x => x.Nickname, nickname)
                .Set(x => x.Bio, bio)
                .Set(x => x.Phone, phone)
                .Set(x => x.AvatarUrl, avatar)
                .Set(x => x.Username, username)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);
            
            await _usersCollection.UpdateOneAsync(

                x => x.Id == id,
                update
            );

            return Ok(new
            {
                success = true,
                message = "Information updated"
            });
        }
    }
}
