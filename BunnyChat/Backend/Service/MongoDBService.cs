//quan ly ket noi mongo //
using MongoDB.Driver;
using BunnyChat.Models;


namespace BunnyChat.Service
{
    public class MongoDbService
    {
        private readonly IConfiguration _configuration;
        private readonly IMongoDatabase _database;

        private readonly IMongoCollection<Message> _messages;
        private readonly IMongoCollection<Conversation> _conversations;
        private readonly IMongoCollection<Friend> _friends;

        public MongoDbService(IConfiguration configuration)
        {
            _configuration = configuration;

            var connectionString = _configuration.GetConnectionString("DbConnection");
            Console.WriteLine($"Connection String = {connectionString}");
            var mongoClient = new MongoClient(connectionString);
            _database = mongoClient.GetDatabase("chatapp");
            _messages = _database.GetCollection<Message>("messages");
            _conversations = _database.GetCollection<Conversation>("conversations");
            _friends = _database.GetCollection<Friend>("friends");
        }
        public IMongoDatabase Database => _database;

        //tạo index cho collection Messages trong MongoDB nhằm:
            // Tăng tốc độ tìm kiếm tin nhắn theo ConversationId.
            // Tăng tốc độ sắp xếp tin nhắn theo CreatedAt (mới nhất → cũ nhất).
            // Giúp tải lịch sử chat nhanh hơn khi số lượng tin nhắn lớn.
        public async Task CreateIndexesAsync()
        {
            await DeleteDocumentForRefreshToken();

            // ConversationId ↑     CreatedAt ↓     { ConversationId: 1, CreatedAt: -1 }
                //MongoDB lọc tin nhắn theo ConversationId.
                // Sau đó sắp xếp theo CreatedAt mới nhất trước.
            var messageIndex = Builders<Message>.IndexKeys
                .Ascending(x => x.ConversationId)
                .Descending(x => x.CreatedAt);
                // => {     ConversationId: 1,  CreatedAt: -1   }

            // Tạo index thật trong MongoDB. Nếu chưa tồn tại -> tạo mới. Nếu có thì bỏ qua
            await _messages.Indexes.CreateOneAsync(
                new CreateIndexModel<Message>(messageIndex)
            );

            //Index cho conversations   { "Participants.UserId": 1, LastMessageAt: -1 }
                //MongoDB tìm các conversation mà user hiện tại nằm trong Participants.
                // Sau đó sort theo LastMessageAt để hội thoại có tin nhắn mới nhất nằm trên cùng.

            var conversationIndex = Builders<Conversation>.IndexKeys
                .Ascending("Participants.UserId")
                .Descending(x => x.LastMessageAt);

            await _conversations.Indexes.CreateOneAsync(
                new CreateIndexModel<Conversation>(conversationIndex)
            );
            // Index cho friends    { SenderId: 1, ReceiveId: 1 }
                // Khi gửi lời mời kết bạn, backend cần kiểm tra giữa 2 user đã có lời mời hoặc đã là bạn chưa.
                // Index giúp tìm nhanh theo cặp SenderId và ReceiveId.
            var friendIndex = Builders<Friend>.IndexKeys
                .Ascending(x => x.SenderId)
                .Ascending(x => x.ReceiveId);

            await _friends.Indexes.CreateOneAsync(
                new CreateIndexModel<Friend>(friendIndex)
            );
        }

        //xoa session khi refreshToken het han
        private async Task DeleteDocumentForRefreshToken()
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

            await sessionCollection.Indexes.CreateOneAsync(ttlIndex);
        }
    }

}
