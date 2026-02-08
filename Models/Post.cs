using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
namespace BackEnd.Models;

public class Post(string title, string authorId, DateTime createdAt, string channelId, string content)
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string PostId { get; set; } = ObjectId.GenerateNewId().ToString();
    
    [NotNull]
    public string Title { get; set; } = title;
    
    [BsonRepresentation(BsonType.ObjectId)]
    public string AuthorId { get; set; } = authorId;
    
    [NotNull] 
    public DateTime CreatedAt { get; set; } = createdAt;
    public DateTime? UpdatedAt { get; set; } = null;
    
    [BsonRepresentation(BsonType.ObjectId)]
    public string ChannelId { get; set; } = channelId;
    
    public string Content { get; set; } = content;

    public bool IsArchived { get; set; } = false;
    
}