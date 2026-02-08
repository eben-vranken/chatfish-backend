using BackEnd.DTOs;
using BackEnd.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace BackEnd.Services;

public class ChannelService
{
    private readonly IMongoCollection<Channel> _channelCollection;
    private readonly PostService _postService;

    public ChannelService(
        IOptions<ChatfishDatabaseSettings> chatfishDatabaseSettings, PostService postService)
    {
        var mongoClient = new MongoClient(
            chatfishDatabaseSettings.Value.ConnectionString);

        var mongoDatabase = mongoClient.GetDatabase(
            chatfishDatabaseSettings.Value.DatabaseName);

        _channelCollection = mongoDatabase.GetCollection<Channel>(
            chatfishDatabaseSettings.Value.ChannelsCollectionName);

        _postService = postService;
    }

    public async Task<List<Channel>> GetAll() =>
        await _channelCollection.Find(_ => true).ToListAsync();

    public async Task<Channel?> Get(string id) =>
        await _channelCollection.Find(x => x.ChannelId == id).FirstOrDefaultAsync();

    public async Task<Channel> Add(ChannelCreateRequest channelRequestDto)
    {
        var newChannel = new Channel(
            channelRequestDto.ChannelName,
            channelRequestDto.ChannelDescription,
            channelRequestDto.ScenarioId);
        await _channelCollection.InsertOneAsync(newChannel);
        return newChannel;
    }

    public async Task Update(string id, Channel updatedChannel) =>
        await _channelCollection.ReplaceOneAsync(x => x.ChannelId == id, updatedChannel);

    public async Task Delete(string id) =>
        await _channelCollection.DeleteOneAsync(x => x.ChannelId == id);

    public async Task<List<Channel>> GetChannelsByScenarioId(string scenarioId) =>
        await _channelCollection.Find(x => x.ScenarioId == scenarioId).ToListAsync();

    public async Task DeleteChannelsByScenarioId(string scenarioId)
    {
        var channels = await GetChannelsByScenarioId(scenarioId);

        foreach (var channel in channels)
        {
            if (string.IsNullOrEmpty(channel.ChannelId))
                continue;

            await _postService.DeletePostsByChannelId(channel.ChannelId);

            await Delete(channel.ChannelId);
        }
    }
}