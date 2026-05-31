using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
namespace BackEnd.Models;

public class UserTimeout(string userId, string issuedById, DateTime endsAt)
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string TimeoutId { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = userId;

    [BsonRepresentation(BsonType.ObjectId)]
    public string IssuedById { get; set; } = issuedById;

    public DateTime EndsAt { get; set; } = endsAt;

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
}
