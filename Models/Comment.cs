using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BackEnd.Models;

public class Comment(string content, string authorId, string postId, DateTime createdAt)
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? CommentId { get; set; }

    public string Content { get; set; } = content;

    [BsonRepresentation(BsonType.ObjectId)]
    public string AuthorId { get; set; } = authorId;

    [BsonRepresentation(BsonType.ObjectId)]
    public string PostId { get; set; } = postId;

    public DateTime CreatedAt { get; set; } = createdAt;
    
    public DateTime? UpdatedAt { get; set; } = null;
}