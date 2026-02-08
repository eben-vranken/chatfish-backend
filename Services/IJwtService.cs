using BackEnd.Models;
using MongoDB.Bson;

namespace BackEnd.Services;

public interface IJwtService
{
    string GenerateToken(User user);
    public ObjectId? ValidateJwtToken(string? token);
}