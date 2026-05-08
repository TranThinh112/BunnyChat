//quan ly ket noi mongo //
using MongoDB.Driver;
using WebMoi.Models;


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
        }
        public IMongoDatabase Database => _database;
    }
}