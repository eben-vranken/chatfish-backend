namespace BackEnd.DTOs;

public class StoryMessageCreateRequest
{
    public string? TextContent { get; set; }
    public string? FileContent { get; set; }
    public DateTime PlannedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public required string ChatId { get; set; }
    public required string CharacterId { get; set; }
}

