namespace BackEnd.Services;
using BackEnd.DTOs;
using BackEnd.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using BCrypt.Net;

public class UserService
{
    private readonly IMongoCollection<User> _usersCollection;
    
    private readonly JwtService _jwtService;
    
    public UserService(
        IOptions<ChatfishDatabaseSettings> chatfishDatabaseSettings, JwtService jwtService)
    {
        _jwtService = jwtService;
        
        var mongoClient = new MongoClient(
            chatfishDatabaseSettings.Value.ConnectionString);

        var mongoDatabase = mongoClient.GetDatabase(
            chatfishDatabaseSettings.Value.DatabaseName);

        _usersCollection = mongoDatabase.GetCollection<User>(
            chatfishDatabaseSettings.Value.UsersCollectionName);
    }
    public async Task<List<User>> GetAll() =>
        await _usersCollection.Find(_ => true).ToListAsync();
    
    public async Task<User?> GetById(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        
        return await _usersCollection.Find(x => x.UserId == id).FirstOrDefaultAsync();
    }

    public async Task<User?> GetByEmail(string email) =>
        await _usersCollection.Find(x => x.Email == email).FirstOrDefaultAsync();

    public async Task<User> Add(UserCreateRequest userCreateRequest)
    {
        var newUser = new User(
            userCreateRequest.Username,
            userCreateRequest.Email,
            BCrypt.HashPassword(userCreateRequest.Password),
            userCreateRequest.Role);
        await _usersCollection.InsertOneAsync(newUser);
        return newUser;
    }

    public async Task Update(string id, User updatedUser) =>
        await _usersCollection.ReplaceOneAsync(x => x.UserId == id, updatedUser);
    
    public async Task Delete(string id) =>
        await _usersCollection.DeleteOneAsync(x => x.UserId == id);
    
    public async Task<string> Login(string email, string password)
    {
        var user = await GetByEmail(email);
        if (user == null)
            return null;

        if (!BCrypt.Verify(password, user.Password))
            return null;
        
        return _jwtService.GenerateToken(user);
    }
}
