public class StoryMessageUpdateRequest
{
    public string? TextContent { get; set; }
    public IFormFile? FileAttachment { get; set; }
    public DateTime PlannedAt { get; set; }
    public string ChatId { get; set; }
    public string CharacterId { get; set; }
}
