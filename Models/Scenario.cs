using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BackEnd.Models;

public class Scenario(string name, string description, string createdBy)
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? ScenarioId { get; set; }

    public string Name { get; set; } = name;

    public string Description { get; set; } = description;

    [BsonRepresentation(BsonType.ObjectId)]
    public string CreatedBy { get; set; } = createdBy;
}
