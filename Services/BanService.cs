using BackEnd.DTOs;
using BackEnd.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace BackEnd.Services;

public class BanService
{
    private readonly IMongoCollection<Ban> _banCollection;
    private readonly UserService _userService;

    public BanService(
        IOptions<ChatfishDatabaseSettings> chatfishDatabaseSettings,
        UserService userService)
    {
        var mongoClient = new MongoClient(chatfishDatabaseSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(chatfishDatabaseSettings.Value.DatabaseName);
        _banCollection = mongoDatabase.GetCollection<Ban>(chatfishDatabaseSettings.Value.BansCollectionName);
        _userService = userService;
    }

    public async Task<Ban?> GetActive(string userId) =>
        await _banCollection.Find(b => b.UserId == userId && b.IsActive).FirstOrDefaultAsync();

    public async Task<BanResponse?> GetActiveResponse(string userId)
    {
        var ban = await GetActive(userId);
        if (ban == null) return null;
        var issuer = await _userService.GetById(ban.IssuedById);
        return ToResponse(ban, issuer?.Username ?? "moderator");
    }

    public async Task<List<BanResponse>> GetAllActive()
    {
        var bans = await _banCollection.Find(b => b.IsActive).ToListAsync();
        var result = new List<BanResponse>();
        foreach (var ban in bans)
        {
            var issuer = await _userService.GetById(ban.IssuedById);
            result.Add(ToResponse(ban, issuer?.Username ?? "moderator"));
        }
        return result;
    }

    public async Task<BanResponse> Create(string targetUserId, string issuedById, string reason)
    {
        // Deactiveer eventuele eerdere actieve bans voor deze gebruiker
        await _banCollection.UpdateManyAsync(
            b => b.UserId == targetUserId && b.IsActive,
            Builders<Ban>.Update.Set(b => b.IsActive, false).Set(b => b.LiftedAt, DateTime.UtcNow));

        var ban = new Ban(targetUserId, issuedById, reason);
        await _banCollection.InsertOneAsync(ban);

        var issuer = await _userService.GetById(issuedById);
        return ToResponse(ban, issuer?.Username ?? "moderator");
    }

    public async Task Lift(string targetUserId, string liftedById)
    {
        await _banCollection.UpdateManyAsync(
            b => b.UserId == targetUserId && b.IsActive,
            Builders<Ban>.Update
                .Set(b => b.IsActive, false)
                .Set(b => b.LiftedAt, DateTime.UtcNow)
                .Set(b => b.LiftedById, liftedById));
    }

    private static BanResponse ToResponse(Ban ban, string issuerName) => new()
    {
        BanId = ban.BanId,
        UserId = ban.UserId,
        IssuedById = ban.IssuedById,
        IssuedByUsername = issuerName,
        Reason = ban.Reason,
        BannedAt = ban.BannedAt
    };
}
