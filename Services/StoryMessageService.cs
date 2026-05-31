namespace BackEnd.Services;

using BackEnd.Models;
using BackEnd.DTOs;
using MongoDB.Driver;
using Microsoft.Extensions.Options;

public class StoryMessageService
{
    private readonly IMongoCollection<StoryMessage> _storyMessagesCollection;
    private readonly IMongoCollection<Chat> _chatCollection;
    private readonly IMongoCollection<User> _userCollection;
    private readonly IMongoCollection<Character> _characterCollection;
    private readonly WebSocketManager _webSocketManager;
    private readonly PushNotificationService _pushNotificationService;

    public StoryMessageService(IOptions<ChatfishDatabaseSettings> chatfishDatabaseSettings, WebSocketManager webSocketManager, PushNotificationService pushNotificationService)
    {
        var mongoClient = new MongoClient(
            chatfishDatabaseSettings.Value.ConnectionString);

        var mongoDatabase = mongoClient.GetDatabase(
            chatfishDatabaseSettings.Value.DatabaseName);

        _storyMessagesCollection = mongoDatabase.GetCollection<StoryMessage>(
            chatfishDatabaseSettings.Value.StoryMessagesCollectionName);

        _chatCollection = mongoDatabase.GetCollection<Chat>(
            chatfishDatabaseSettings.Value.ChatsCollectionName);

        _userCollection = mongoDatabase.GetCollection<User>(
            chatfishDatabaseSettings.Value.UsersCollectionName);

        _characterCollection = mongoDatabase.GetCollection<Character>(
            chatfishDatabaseSettings.Value.CharactersCollectionName);

        _webSocketManager = webSocketManager;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<List<StoryMessage>> GetAll() =>
        await _storyMessagesCollection.Find(_ => true).ToListAsync();

    public async Task<StoryMessage?> Get(string id) =>
        await _storyMessagesCollection.Find(x => x.StoryMessageId == id).FirstOrDefaultAsync();

    public async Task<StoryMessage> Add(StoryMessageCreateRequest storyMessageCreateRequest)
    {
        var storyMessage = new StoryMessage(
            storyMessageCreateRequest.TextContent ?? string.Empty,
            storyMessageCreateRequest.FileContent ?? string.Empty,
            storyMessageCreateRequest.PlannedAt,
            storyMessageCreateRequest.CreatedAt,
            storyMessageCreateRequest.UpdatedAt,
            storyMessageCreateRequest.ChatId,
            storyMessageCreateRequest.CharacterId);
        await _storyMessagesCollection.InsertOneAsync(storyMessage);
        return storyMessage;
    }

    public async Task<StoryMessage?> Update(string id, StoryMessage storyMessage)
    {
        var existing = await Get(id);
        if (existing == null)
            return null;

        storyMessage.StoryMessageId = existing.StoryMessageId;
        await _storyMessagesCollection.ReplaceOneAsync(x => x.StoryMessageId == id, storyMessage);
        return storyMessage;
    }

    public async Task Delete(string id)
    {
        await _storyMessagesCollection.DeleteOneAsync(x => x.StoryMessageId == id);
    }

    public async Task<List<StoryMessage>> GetByChatId(string chatId)
    {
        return await _storyMessagesCollection
            .Find(m => m.ChatId == chatId)
            .SortBy(m => m.PlannedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Public-facing: only return messages that have been marked as sent.
    /// Used by the frontend theatre chat so future/unsent messages are hidden.
    /// </summary>
    public async Task<List<StoryMessage>> GetSentByChatId(string chatId)
    {
        var filter = Builders<StoryMessage>.Filter.And(
            Builders<StoryMessage>.Filter.Eq(m => m.ChatId, chatId),
            Builders<StoryMessage>.Filter.Eq(m => m.Sent, true)
        );

        return await _storyMessagesCollection
            .Find(filter)
            .SortBy(m => m.PlannedAt)
            .ToListAsync();
    }

    public async Task<List<StoryMessage>> GetByChatIdSince(string chatId, DateTime since)
    {
        // Ensure the since date is in UTC
        var sinceUtc = since.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(since, DateTimeKind.Utc)
            : since.ToUniversalTime();

        var now = DateTime.UtcNow;

        var filter = Builders<StoryMessage>.Filter.And(
            Builders<StoryMessage>.Filter.Eq(m => m.ChatId, chatId),
            Builders<StoryMessage>.Filter.Gt(m => m.PlannedAt, sinceUtc),
            Builders<StoryMessage>.Filter.Lte(m => m.PlannedAt, now)
        );

        return await _storyMessagesCollection
            .Find(filter)
            .SortBy(m => m.PlannedAt)
            .ToListAsync();
    }

    public async Task<List<StoryMessage>> GetDueMessagesAsync()
    {
        var now = DateTime.UtcNow;

        var filter = Builders<StoryMessage>.Filter.And(
            Builders<StoryMessage>.Filter.Lte(m => m.PlannedAt, now),
            Builders<StoryMessage>.Filter.Eq(m => m.Sent, false)
        );

        return await _storyMessagesCollection.Find(filter).ToListAsync();
    }

    private async Task MarkAsSentAsync(StoryMessage message)
    {
        var update = Builders<StoryMessage>.Update
            .Set(m => m.Sent, true)
            .Set(m => m.SentAt, DateTime.UtcNow);

        await _storyMessagesCollection.UpdateOneAsync(
            m => m.StoryMessageId == message.StoryMessageId,
            update
        );
    }

    public async Task UpdateMessageShift(StoryMessage msg, DateTime newTime)
    {
        var update = Builders<StoryMessage>.Update
            .Set(m => m.PlannedAt, newTime)
            .Set(m => m.UpdatedAt, DateTime.UtcNow)
            .Set(m => m.Sent, false)
            .Set(m => m.SentAt, null);

        await _storyMessagesCollection.UpdateOneAsync(
            x => x.StoryMessageId == msg.StoryMessageId,
            update
        );
    }

    public async Task<List<StoryMessage>> GetMessagesByChatId(string chatId)
    {
        return await _storyMessagesCollection
            .Find(m => m.ChatId == chatId)
            .ToListAsync();
    }

    public async Task UpdatePlannedAt(string id, DateTime newTime)
    {
        var update = Builders<StoryMessage>.Update
            .Set(m => m.PlannedAt, newTime)
            .Set(m => m.Sent, false)
            .Set(m => m.SentAt, null)
            .Set(m => m.UpdatedAt, DateTime.UtcNow);

        await _storyMessagesCollection.UpdateOneAsync(
            m => m.StoryMessageId == id,
            update);
    }


    public async Task<List<StoryMessage>> ProcessDueMessagesAsync()
    {
        // Filter to unsent messages in the past
        var filter = Builders<StoryMessage>.Filter.Lte(m => m.PlannedAt, DateTime.UtcNow) &
                     (Builders<StoryMessage>.Filter.Eq(m => m.Sent, false) | Builders<StoryMessage>.Filter.Eq(m => m.SentAt, null));
        var dueMessages = await _storyMessagesCollection.Find(filter).ToListAsync();

        foreach (var msg in dueMessages)
        {
            // Fill in linked documents
            var chat = await _chatCollection.Find(c => c.ChatId == msg.ChatId).FirstOrDefaultAsync();
            var character = !string.IsNullOrEmpty(msg.CharacterId)
                ? await _characterCollection.Find(c => c.CharacterId == msg.CharacterId).FirstOrDefaultAsync()
                : null;

            var fullMessage = new
            {
                msg.StoryMessageId,
                Chat = chat,
                Character = character,
                msg.TextContent,
                msg.FileContent,
                msg.PlannedAt,
                msg.Sent,
                msg.SentAt
            };

            await _webSocketManager.BroadcastToAllAsync(new
            {
                type = "storyMessage",
                message = fullMessage
            });

            // Send push notification
            var characterName = character?.Name ?? "New message";
            var previewText = msg.TextContent?.Length > 100 
                ? msg.TextContent[..100] + "..." 
                : msg.TextContent ?? "You have a new message";
            await _pushNotificationService.SendToAllAsync(characterName, previewText);

            await MarkAsSentAsync(msg);
        }
        return dueMessages;
    }

    public async Task DeleteMessagesByChatId(string chatId)
    {
        if (string.IsNullOrEmpty(chatId))
            return;

        var filter = Builders<StoryMessage>.Filter.Eq(m => m.ChatId, chatId);
        await _storyMessagesCollection.DeleteManyAsync(filter);
    }
}
