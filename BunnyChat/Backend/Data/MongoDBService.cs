//quan ly ket noi mongo //
using MongoDB.Driver;
using BunnyChat.Models;
using BunnyChat.Models.Entities;


namespace BunnyChat.Data
{
    public class MongoDbService
    {
        private readonly IConfiguration _configuration;
        private readonly IMongoDatabase _database;

        public MongoDbService(IConfiguration configuration)
        {
            _configuration = configuration;

            var connectionString = _configuration.GetConnectionString("DbConnection");
            Console.WriteLine($"Connection String = {connectionString}");
            var mongoClient = new MongoClient(connectionString);
            _database = mongoClient.GetDatabase("chatapp");

            DeleteDocumentForRefreshToken();
        }
        public IMongoDatabase Database => _database;


        private void DeleteDocumentForRefreshToken()
            {
                //Lấy collection Sesion
                var sessionCollection = Database.GetCollection<Session>("sessions");

                var ttlIndex = new CreateIndexModel<Session>(
                    Builders<Session>.IndexKeys.Ascending(s => s.ExpiresAt),

                    new CreateIndexOptions
                    {
                        // xóa NGAY khi tới thời điểm ExpiresAt
                        ExpireAfter = TimeSpan.Zero
                    }
                );

                sessionCollection.Indexes.CreateOne(ttlIndex);
            }
    }
    
}