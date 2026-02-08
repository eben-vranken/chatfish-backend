using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BackEnd.Models
{
    public class Character(string name, string profilePicture, string scenarioId)
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? CharacterId { get; set; }

        public string Name { get; set; } = name;
        
        public string ProfilePicture { get; set; } = profilePicture;
        
        [BsonRepresentation(BsonType.ObjectId)] 
        public string ScenarioId { get; set; } = scenarioId;
    }
}
