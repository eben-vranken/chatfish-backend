using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
namespace BackEnd.Models;

public class Ban(string userId, string issuedById, string reason)
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string BanId { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = userId;

    [BsonRepresentation(BsonType.ObjectId)]
    public string IssuedById { get; set; } = issuedById;

    public string Reason { get; set; } = reason;

    public DateTime BannedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    public DateTime? LiftedAt { get; set; } = null;

    [BsonRepresentation(BsonType.ObjectId)]
    public string? LiftedById { get; set; } = null;
}
