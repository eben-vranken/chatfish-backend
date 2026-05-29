using BackEnd.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace BackEnd.Services;

public class TicketService
{
    private readonly IMongoCollection<Ticket> _ticketsCollection;

    public TicketService(IOptions<ChatfishDatabaseSettings> chatfishDatabaseSettings)
    {
        var mongoClient = new MongoClient(chatfishDatabaseSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(chatfishDatabaseSettings.Value.DatabaseName);
        _ticketsCollection = mongoDatabase.GetCollection<Ticket>(
            chatfishDatabaseSettings.Value.TicketsCollectionName);
    }

    public async Task<bool> HasTicket(string userId, string scenarioId)
    {
        var filter = Builders<Ticket>.Filter.Eq(t => t.UserId, userId) &
                     Builders<Ticket>.Filter.Eq(t => t.ScenarioId, scenarioId);
        return await _ticketsCollection.Find(filter).AnyAsync();
    }

    public async Task<List<Ticket>> GetByUser(string userId)
    {
        var filter = Builders<Ticket>.Filter.Eq(t => t.UserId, userId);
        return await _ticketsCollection.Find(filter).ToListAsync();
    }

    public async Task<Ticket> Buy(string userId, string scenarioId)
    {
        var existing = await _ticketsCollection
            .Find(Builders<Ticket>.Filter.Eq(t => t.UserId, userId) &
                  Builders<Ticket>.Filter.Eq(t => t.ScenarioId, scenarioId))
            .FirstOrDefaultAsync();

        if (existing is not null)
        {
            return existing;
        }

        var ticket = new Ticket
        {
            UserId = userId,
            ScenarioId = scenarioId,
            PurchasedAt = DateTime.UtcNow,
        };
        await _ticketsCollection.InsertOneAsync(ticket);
        return ticket;
    }
}
