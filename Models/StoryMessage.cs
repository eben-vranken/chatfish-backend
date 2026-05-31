using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BackEnd.Models;
public class StoryMessage
{
public StoryMessage() {}

    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? StoryMessageId { get; set; }
    public string? TextContent { get; set; }
    public string? FileContent { get; set; }

    /// <summary>
    /// MIME-type van de bijlage (bv. "image/png", "video/mp4"). Niet opgeslagen in
    /// de database; wordt bij het ophalen ingevuld vanuit de MinIO-objectmetadata
    /// zodat de frontend weet hoe de media gerenderd moet worden.
    /// </summary>
    [BsonIgnore]
    public string? FileContentType { get; set; }
    public DateTime PlannedAt
    {
        get => plannedAt;
        set => plannedAt = DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
    public DateTime CreatedAt
    {
        get => createdAt;
        set => createdAt = DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
    public DateTime? UpdatedAt
    {
        get => updatedAt;
        set => updatedAt = value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
    }
    [BsonRepresentation(BsonType.ObjectId)]
    public string ChatId { get; set; }
    [BsonRepresentation(BsonType.ObjectId)]
    public string CharacterId { get; set; }
    private DateTime plannedAt;
    private DateTime createdAt;
    private DateTime? updatedAt;
    public bool Sent { get; set; } = false;
    public DateTime? SentAt { get; set; }


    
    public StoryMessage(string textContent, string fileContent, DateTime plannedAt, DateTime createdAt, DateTime? updatedAt, string chatId, string characterId)
    {
        TextContent = textContent;
        FileContent = fileContent;
        ChatId = chatId;
        CharacterId = characterId;

        PlannedAt = plannedAt;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }
}