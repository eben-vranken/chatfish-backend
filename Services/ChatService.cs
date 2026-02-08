using BackEnd.DTOs;
using BackEnd.Models;
using BackEnd.Util;
using dotenv.net.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Minio;
using MongoDB.Driver;
using System.Security.Cryptography;

namespace BackEnd.Services;

public class ChatService
{

    private readonly IMongoCollection<Chat> _chatsCollection;
    private readonly IMinioClient _minioClient;
    private readonly ILogger<ChatService> _logger;
    private readonly string _bucketName;
    private readonly StoryMessageService _storyMessageService;

    public ChatService(
        IOptions<ChatfishDatabaseSettings> chatfishDatabaseSettings,
        IMinioClient minioClient,
        ILogger<ChatService> logger, StoryMessageService storyMessageService)
    {
        var mongoClient = new MongoClient(
            chatfishDatabaseSettings.Value.ConnectionString);

        var mongoDatabase = mongoClient.GetDatabase(
            chatfishDatabaseSettings.Value.DatabaseName);

        _chatsCollection = mongoDatabase.GetCollection<Chat>(
            chatfishDatabaseSettings.Value.ChatsCollectionName);

        _minioClient = minioClient;
        _logger = logger;
        _bucketName = EnvReader.TryGetStringValue("MINIO_BUCKET", out var bucket) && !string.IsNullOrWhiteSpace(bucket)
            ? bucket
            : "chats";

        _storyMessageService = storyMessageService;
    }

    public async Task<List<Chat>> GetAll() =>
        await _chatsCollection.Find(_ => true).ToListAsync();

    public async Task<Chat?> Get(string id) =>
        await _chatsCollection.Find(x => x.ChatId == id).FirstOrDefaultAsync();

    public async Task<Chat> Add(Chat chat)
    {
        await _chatsCollection.InsertOneAsync(chat);
        return chat;
    }

    public async Task Update(string id, Chat updatedChat) =>
        await _chatsCollection.ReplaceOneAsync(x => x.ChatId == id, updatedChat);

    public async Task Delete(string id)
    {
        var chat = await _chatsCollection
            .Find(c => c.ChatId == id)
            .FirstOrDefaultAsync();

        if (chat == null)
            throw new KeyNotFoundException("Chat niet gevonden.");

        await _chatsCollection.DeleteOneAsync(c => c.ChatId == id);
    }

    public async Task<List<Chat>> GetChatsByScenario(string scenarioId)
    {
        var filter = Builders<Chat>.Filter.Eq(c => c.ScenarioId, scenarioId);
        return await _chatsCollection.Find(filter).ToListAsync();
    }

    public async Task<bool> IsProfilePictureUsedByOtherChats(string profilePictureHash, string excludeChatId)
    {
        if (string.IsNullOrWhiteSpace(profilePictureHash))
            return false;

        var filter = Builders<Chat>.Filter.And(
            Builders<Chat>.Filter.Eq(c => c.ProfilePicture, profilePictureHash),
            Builders<Chat>.Filter.Ne(c => c.ChatId, excludeChatId)
        );

        var count = await _chatsCollection.CountDocumentsAsync(filter);
        return count > 0;
    }

    public async Task<string?> UploadProfilePictureAsync(IFormFile? profilePicture)
    {
        if (profilePicture is not { Length: > 0 })
            return null;

        await using var memoryStream = new MemoryStream();
        await profilePicture.CopyToAsync(memoryStream);
        var hashBytes = SHA256.HashData(memoryStream.ToArray());
        var fileHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var contentType = string.IsNullOrWhiteSpace(profilePicture.ContentType)
            ? "application/octet-stream"
            : profilePicture.ContentType!;

        await MinioUtils.PutObjectAsync(_minioClient, _bucketName, fileHash, memoryStream, contentType);
        return fileHash;
    }

    public async Task DeleteProfilePictureIfUnusedAsync(string? profilePictureHash, string chatId)
    {
        if (string.IsNullOrWhiteSpace(profilePictureHash))
            return;

        try
        {
            var usedByOtherChats = await IsProfilePictureUsedByOtherChats(profilePictureHash, chatId);

            if (usedByOtherChats)
            {
                _logger.LogInformation("Profile picture {ProfilePictureHash} is still used by other entities, skipping deletion", profilePictureHash);
            }
            else
            {
                await MinioUtils.RemoveObjectAsync(_minioClient, _bucketName, profilePictureHash);
                _logger.LogInformation("Deleted profile picture {ProfilePictureHash} from Minio after chat {ChatId} deletion", profilePictureHash, chatId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete profile picture {ProfilePictureHash} from Minio for chat {ChatId}", profilePictureHash, chatId);
        }
    }

    public async Task DeleteChatsByScenario(string scenarioId)
    {
        var chats = await GetChatsByScenario(scenarioId);

        foreach (var chat in chats)
        {
            if (string.IsNullOrEmpty(chat.ChatId))
                continue;

            await _storyMessageService.DeleteMessagesByChatId(chat.ChatId);

            await Delete(chat.ChatId);
        }
    }


}

