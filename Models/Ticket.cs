using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BackEnd.Models;

public class Ticket
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? TicketId { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string ScenarioId { get; set; } = string.Empty;

    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;
}
