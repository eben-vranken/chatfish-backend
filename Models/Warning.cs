using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
namespace BackEnd.Models;

public class Warning(string targetUserId, string issuedById, string reason)
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string WarningId { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(BsonType.ObjectId)]
    public string TargetUserId { get; set; } = targetUserId;

    [BsonRepresentation(BsonType.ObjectId)]
    public string IssuedById { get; set; } = issuedById;

    public string Reason { get; set; } = reason;

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
}
