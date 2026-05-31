namespace BackEnd.Models;

public class ChatfishDatabaseSettings
{
    public string ConnectionString { get; set; } = null!;

    public string DatabaseName { get; set; } = null!;
    
    public string UsersCollectionName { get; set; } = null!;

    public string ChatsCollectionName { get; set; } = null!;
    
    public string CommentsCollectionName { get; set; } = null!;

    public string StoryMessagesCollectionName { get; set; } = null!;
    
    public string PostsCollectionName { get; set; } = null!;
    
    public string ChannelsCollectionName { get; set; } = null!;
    
    public string CharactersCollectionName { get; set; } = null!;

    public string ScenariosCollectionName { get; set; } = null!;
    
    public string PushSubscriptionsCollectionName { get; set; } = null!;

    public string TicketsCollectionName { get; set; } = "Tickets";

    public string WarningsCollectionName { get; set; } = "Warnings";

    public string TimeoutsCollectionName { get; set; } = "Timeouts";

    public string BansCollectionName { get; set; } = "Bans";
}