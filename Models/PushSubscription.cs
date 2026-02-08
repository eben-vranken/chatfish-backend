using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BackEnd.Models;

public class PushSubscription(string userId, string endpoint, string p256dh, string auth)
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? SubscriptionId { get; set; }
    
    public string UserId { get; set; } = userId;
    
    public string Endpoint { get; set; } = endpoint;
    
    public string P256dh { get; set; } = p256dh;
    
    public string Auth { get; set; } = auth;
}
