namespace BackEnd.Services;

using BackEnd.Models;
using dotenv.net.Utilities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using WebPush;

using ModelPushSubscription = BackEnd.Models.PushSubscription;

public class PushNotificationService
{
    private readonly IMongoCollection<ModelPushSubscription> _subscriptionsCollection;
    private readonly VapidDetails _vapidDetails;
    private readonly WebPushClient _webPushClient;

    public PushNotificationService(IOptions<ChatfishDatabaseSettings> chatfishDatabaseSettings)
    {
        var mongoClient = new MongoClient(chatfishDatabaseSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(chatfishDatabaseSettings.Value.DatabaseName);
        _subscriptionsCollection = mongoDatabase.GetCollection<ModelPushSubscription>(
            chatfishDatabaseSettings.Value.PushSubscriptionsCollectionName);

        _vapidDetails = new VapidDetails(
            EnvReader.GetStringValue("VAPID_SUBJECT"),
            EnvReader.GetStringValue("VAPID_PUBLIC_KEY"),
            EnvReader.GetStringValue("VAPID_PRIVATE_KEY")
        );

        _webPushClient = new WebPushClient();
    }

    public string GetPublicKey() => _vapidDetails.PublicKey;

    public async Task<ModelPushSubscription> Subscribe(string userId, string endpoint, string p256dh, string auth)
    {
        Console.WriteLine($"[PushNotificationService] {DateTime.Now}: Subscribing user {userId} to push notifications...");
        // Remove existing subscription for this endpoint (re-subscribe case)
        await _subscriptionsCollection.DeleteManyAsync(s => s.Endpoint == endpoint);

        var subscription = new ModelPushSubscription(userId, endpoint, p256dh, auth);
        await _subscriptionsCollection.InsertOneAsync(subscription);
        Console.WriteLine($"[PushNotificationService] {DateTime.Now}: User {userId} subscribed successfully.");
        return subscription;
    }

    public async Task Unsubscribe(string endpoint)
    {
        Console.WriteLine($"[PushNotificationService] {DateTime.Now}: Unsubscribing endpoint {endpoint}...");
        await _subscriptionsCollection.DeleteManyAsync(s => s.Endpoint == endpoint);
        Console.WriteLine($"[PushNotificationService] {DateTime.Now}: Unsubscribed successfully.");
    }

    public async Task<ModelPushSubscription?> GetByEndpoint(string endpoint)
    {
        return await _subscriptionsCollection.Find(s => s.Endpoint == endpoint).FirstOrDefaultAsync();
    }

    public async Task SendToAllAsync(string title, string body, string? url = null)
    {
        var subscriptions = await _subscriptionsCollection.Find(_ => true).ToListAsync();
        Console.WriteLine($"[PushNotificationService] {DateTime.Now}: Sending push notifications to {subscriptions.Count} subscribers...");
        
        var expiredSubscriptions = new List<string>();
        int successCount = 0;

        foreach (var sub in subscriptions)
        {
            try
            {
                var pushSubscription = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    title,
                    body,
                    url
                });

                await _webPushClient.SendNotificationAsync(pushSubscription, payload, _vapidDetails);
                successCount++;
                Console.WriteLine($"[PushNotificationService] {DateTime.Now}: Successfully sent notification to user {sub.UserId}");
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone ||
                                               ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine($"[PushNotificationService] {DateTime.Now}: Subscription expired for user {sub.UserId}");
                expiredSubscriptions.Add(sub.Endpoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PushNotificationService] {DateTime.Now}: Error sending notification to user {sub.UserId}: {ex.Message}");
            }
        }

        if (expiredSubscriptions.Count > 0)
        {
            Console.WriteLine($"[PushNotificationService] {DateTime.Now}: Cleaning up {expiredSubscriptions.Count} expired subscriptions...");
            await _subscriptionsCollection.DeleteManyAsync(s => expiredSubscriptions.Contains(s.Endpoint));
        }

        Console.WriteLine($"[PushNotificationService] {DateTime.Now}: Finished sending notifications. Success: {successCount}, Expired: {expiredSubscriptions.Count}");
    }
}
