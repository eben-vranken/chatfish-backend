using BackEnd.DTOs;
using BackEnd.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace BackEnd.Services;

public class ScenarioService
{
    private readonly IMongoCollection<Scenario> _scenarioCollection;
    private readonly ChatService _chatService;
    private readonly StoryMessageService _storyMessageService;
    private readonly ChannelService _channelService;
    private readonly PostService _postService;
    private readonly CharacterService _characterService;

    public ScenarioService(
        IOptions<ChatfishDatabaseSettings> chatfishDatabaseSettings, ChatService chatService,
        StoryMessageService storyMessageService, ChannelService channelService,
        PostService postService, CharacterService characterService)
    {
        var mongoClient = new MongoClient(
            chatfishDatabaseSettings.Value.ConnectionString);

        var mongoDatabase = mongoClient.GetDatabase(
            chatfishDatabaseSettings.Value.DatabaseName);

        _scenarioCollection = mongoDatabase.GetCollection<Scenario>(
            chatfishDatabaseSettings.Value.ScenariosCollectionName);

        _chatService = chatService;
        _storyMessageService = storyMessageService;
        _channelService = channelService;
        _postService = postService;
        _characterService = characterService;
    }

    public async Task<List<Scenario>> GetAll() =>
        await _scenarioCollection.Find(_ => true).ToListAsync();

    public async Task<Scenario?> GetById(string id) =>
        await _scenarioCollection.Find(x => x.ScenarioId == id).FirstOrDefaultAsync();

    public async Task<Scenario> Add(ScenarioCreateRequest scenarioCreateRequest)
    {
        if (string.IsNullOrEmpty(scenarioCreateRequest.CreatedBy))
        {
            throw new ArgumentException("CreatedBy cannot be null or empty", nameof(scenarioCreateRequest));
        }

        var newScenario = new Scenario(
            scenarioCreateRequest.Name,
            scenarioCreateRequest.Description,
            scenarioCreateRequest.CreatedBy)
        {
            StartMoment = scenarioCreateRequest.StartMoment,
            DurationMinutes = scenarioCreateRequest.DurationMinutes,
            Price = scenarioCreateRequest.Price,
            SaleStatus = scenarioCreateRequest.SaleStatus,
        };
        await _scenarioCollection.InsertOneAsync(newScenario);
        return newScenario;
    }

    public async Task<bool> Update(string id, Scenario updatedScenario)
    {
        var result = await _scenarioCollection.ReplaceOneAsync(
            s => s.ScenarioId == id,
            updatedScenario
        );

        return result.MatchedCount > 0;
    }

    public async Task<bool> ShiftScenario(string scenarioId, DateTime newFirstMessageTimeUtc)
    {
        var chats = await _chatService.GetChatsByScenario(scenarioId);
        if (chats.Count == 0)
            return false;

        var allMessages = new List<StoryMessage>();

        foreach (var chat in chats)
        {
            if (string.IsNullOrEmpty(chat.ChatId))
                continue;

            var messages = await _storyMessageService.GetMessagesByChatId(chat.ChatId);
            allMessages.AddRange(messages);
        }

        if (allMessages.Count == 0)
            return false;

        var firstMessage = allMessages.OrderBy(m => m.PlannedAt).First();

        foreach (var msg in allMessages)
        {
            var relativeOffset = msg.PlannedAt - firstMessage.PlannedAt;
            var newPlannedTime = newFirstMessageTimeUtc + relativeOffset;

            if (!string.IsNullOrEmpty(msg.StoryMessageId))
                await _storyMessageService.UpdatePlannedAt(msg.StoryMessageId, newPlannedTime);
        }

        var channels = await _channelService.GetChannelsByScenarioId(scenarioId);
        var allPosts = new List<PostResponse>();

        foreach (var channel in channels)
        {
            if (string.IsNullOrEmpty(channel.ChannelId))
                continue;

            var posts = await _postService.GetPostsByNonArchivedChannelId(channel.ChannelId);
            allPosts.AddRange(posts);
        }

        foreach (var post in allPosts)
        {
            if (!string.IsNullOrEmpty(post.PostId))
                await _postService.Archive(post.PostId);
        }
        return true;
    }

    public async Task<bool> DeleteScenarioById(string scenarioId)
    {
        var scenario = await GetById(scenarioId);
        if (scenario == null)
            return false;

        await _chatService.DeleteChatsByScenario(scenarioId);

        await _channelService.DeleteChannelsByScenarioId(scenarioId);

        await _characterService.DeleteCharactersByScenarioId(scenarioId);

        var result = await _scenarioCollection.DeleteOneAsync(
            s => s.ScenarioId == scenarioId
        );

        return result.DeletedCount > 0;
    }
}
