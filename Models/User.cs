using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BackEnd.Models;

public class User(string username, string email, string password, string role)
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? UserId { get; set; }
    
    public string Username { get; set; } = username;

    public string Email { get; set; } = email;
    
    public string Password { get; set; } = password;
    
    public string Role { get; set; } = role;
}
