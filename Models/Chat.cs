using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BackEnd.Models;

public class Chat(string name, string scenarioId, string? profilePicture = null)
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? ChatId { get; set; }

    public string Name { get; set; } = name;

    [BsonRepresentation(BsonType.ObjectId)]
    public string ScenarioId { get; set; } = scenarioId;
    
    public string? ProfilePicture { get; set; } = profilePicture;
}
