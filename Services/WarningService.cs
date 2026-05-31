using BackEnd.DTOs;
using BackEnd.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace BackEnd.Services;

public class WarningService
{
    private readonly IMongoCollection<Warning> _warningCollection;
    private readonly UserService _userService;
    private readonly WebSocketManager _webSocketManager;
    private readonly PushNotificationService _pushNotificationService;

    public WarningService(
        IOptions<ChatfishDatabaseSettings> chatfishDatabaseSettings,
        UserService userService,
        WebSocketManager webSocketManager,
        PushNotificationService pushNotificationService)
    {
        var mongoClient = new MongoClient(chatfishDatabaseSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(chatfishDatabaseSettings.Value.DatabaseName);
        _warningCollection = mongoDatabase.GetCollection<Warning>(chatfishDatabaseSettings.Value.WarningsCollectionName);
        _userService = userService;
        _webSocketManager = webSocketManager;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<WarningResponse> CreateWarning(string targetUserId, string issuedById, string reason)
    {
        var warning = new Warning(targetUserId, issuedById, reason);
        await _warningCollection.InsertOneAsync(warning);

        var issuer = await _userService.GetById(issuedById);
        var issuerName = issuer?.Username ?? "moderator";

        // Real-time WebSocket notification if user is connected
        await _webSocketManager.SendToUserAsync(targetUserId, new
        {
            type = "warning",
            warningId = warning.WarningId,
            reason = warning.Reason,
            issuedByUsername = issuerName,
            issuedAt = warning.IssuedAt
        });

        // Browser push notification if user is subscribed
        await _pushNotificationService.SendToUserAsync(
            targetUserId,
            "Waarschuwing ontvangen",
            $"Een moderator heeft je een waarschuwing gegeven: {reason}"
        );

        return new WarningResponse
        {
            WarningId = warning.WarningId,
            TargetUserId = warning.TargetUserId,
            IssuedById = warning.IssuedById,
            IssuedByUsername = issuerName,
            Reason = warning.Reason,
            IssuedAt = warning.IssuedAt
        };
    }

    public async Task<List<WarningResponse>> GetByTargetUser(string userId)
    {
        var warnings = await _warningCollection
            .Find(w => w.TargetUserId == userId)
            .SortByDescending(w => w.IssuedAt)
            .ToListAsync();

        var result = new List<WarningResponse>();
        foreach (var w in warnings)
        {
            var issuer = await _userService.GetById(w.IssuedById);
            result.Add(new WarningResponse
            {
                WarningId = w.WarningId,
                TargetUserId = w.TargetUserId,
                IssuedById = w.IssuedById,
                IssuedByUsername = issuer?.Username ?? "moderator",
                Reason = w.Reason,
                IssuedAt = w.IssuedAt
            });
        }
        return result;
    }
}
