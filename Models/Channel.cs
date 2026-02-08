using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Diagnostics.CodeAnalysis;

namespace BackEnd.Models;

public class Channel(string channelName, string channelDescription, string scenarioId)
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? ChannelId { get; set; }

    public string ChannelName { get; set; } = channelName;

    public string ChannelDescription { get; set; } = channelDescription;

    [BsonRepresentation(BsonType.ObjectId)] 
    public string ScenarioId { get; set; } = scenarioId;
}
