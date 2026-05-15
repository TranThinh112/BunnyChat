//quan ly ket noi mongo //
using MongoDB.Driver;
using WebMoi.Models;
using WebMoi.Models.Entities;


namespace WebMoi.Data
{
    public class MongoDbService
    {
        private readonly IConfiguration _configuration;
        private readonly IMongoDatabase _database;

        public MongoDbService(IConfiguration configuration)
        {
            _configuration = configuration;

            var connectionString = _configuration.GetConnectionString("DbConnection");
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