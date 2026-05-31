using BackEnd.DTOs;
using BackEnd.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace BackEnd.Services;

public class UserTimeoutService
{
    private readonly IMongoCollection<UserTimeout> _timeoutCollection;
    private readonly UserService _userService;

    public UserTimeoutService(
        IOptions<ChatfishDatabaseSettings> chatfishDatabaseSettings,
        UserService userService)
    {
        var mongoClient = new MongoClient(chatfishDatabaseSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(chatfishDatabaseSettings.Value.DatabaseName);
        _timeoutCollection = mongoDatabase.GetCollection<UserTimeout>(
            chatfishDatabaseSettings.Value.TimeoutsCollectionName);
        _userService = userService;
    }

    /// <summary>
    /// Geeft de actieve (nog niet verlopen) time-out voor een gebruiker terug, of null als er geen is.
    /// </summary>
    public async Task<UserTimeout?> GetActive(string userId)
    {
        var now = DateTime.UtcNow;
        return await _timeoutCollection
            .Find(t => t.UserId == userId && t.EndsAt > now)
            .SortByDescending(t => t.EndsAt)
            .FirstOrDefaultAsync();
    }

    public async Task<UserTimeoutResponse> Create(string targetUserId, string issuedById, int durationMinutes)
    {
        var endsAt = DateTime.UtcNow.AddMinutes(durationMinutes);
        var timeout = new UserTimeout(targetUserId, issuedById, endsAt);
        await _timeoutCollection.InsertOneAsync(timeout);

        var issuer = await _userService.GetById(issuedById);

        return new UserTimeoutResponse
        {
            TimeoutId = timeout.TimeoutId,
            UserId = timeout.UserId,
            IssuedById = timeout.IssuedById,
            IssuedByUsername = issuer?.Username ?? "moderator",
            EndsAt = timeout.EndsAt,
            IssuedAt = timeout.IssuedAt
        };
    }

    public async Task<UserTimeoutResponse?> GetActiveResponse(string userId)
    {
        var timeout = await GetActive(userId);
        if (timeout == null) return null;

        var issuer = await _userService.GetById(timeout.IssuedById);
        return new UserTimeoutResponse
        {
            TimeoutId = timeout.TimeoutId,
            UserId = timeout.UserId,
            IssuedById = timeout.IssuedById,
            IssuedByUsername = issuer?.Username ?? "moderator",
            EndsAt = timeout.EndsAt,
            IssuedAt = timeout.IssuedAt
        };
    }

    /// <summary>Verwijdert alle actieve time-outs voor een gebruiker (opheffing door moderator).</summary>
    public async Task Lift(string targetUserId)
    {
        var now = DateTime.UtcNow;
        await _timeoutCollection.DeleteManyAsync(t => t.UserId == targetUserId && t.EndsAt > now);
    }
}
