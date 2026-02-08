using BackEnd.DTOs;
using BackEnd.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace BackEnd.Services;

public class CharacterService
{
    private readonly IMongoCollection<Character> _characterCollection;

    public CharacterService(
        IOptions<ChatfishDatabaseSettings> chatfishDatabaseSettings)
    {
        var mongoClient = new MongoClient(
            chatfishDatabaseSettings.Value.ConnectionString);

        var mongoDatabase = mongoClient.GetDatabase(
            chatfishDatabaseSettings.Value.DatabaseName);

        _characterCollection = mongoDatabase.GetCollection<Character>(
            chatfishDatabaseSettings.Value.CharactersCollectionName);
    }

    public async Task<Character> Add(CharacterCreateRequest characterCreateRequest, string? profilePictureData)
    {
        var newCharacter = new Character(
            characterCreateRequest.Name,
            profilePictureData ?? string.Empty,
            characterCreateRequest.ScenarioId);
        await _characterCollection.InsertOneAsync(newCharacter);
        return newCharacter;
    }

    public async Task Edit(Character character) =>
        await _characterCollection.ReplaceOneAsync(x => x.CharacterId == character.CharacterId, character);

    public async Task<List<Character>> GetByScenario(string id) =>
        await _characterCollection.Find(x => x.ScenarioId == id).ToListAsync();

    public async Task<List<Character>> GetAll() =>
        await _characterCollection.Find(_ => true).ToListAsync();

    public async Task<Character?> GetById(string id) =>
        await _characterCollection.Find(x => x.CharacterId == id).FirstOrDefaultAsync();

    public async Task Delete(string id) =>
        await _characterCollection.DeleteOneAsync(x => x.CharacterId == id);

    public async Task DeleteCharactersByScenarioId(string scenarioId)
    {
        var characters = await GetByScenario(scenarioId);

        foreach (var character in characters)
        {
            if (string.IsNullOrEmpty(character.CharacterId))
                continue;

            await Delete(character.CharacterId);
        }
    }

}