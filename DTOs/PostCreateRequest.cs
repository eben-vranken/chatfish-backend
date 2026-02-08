namespace BackEnd.DTOs;

public class PostCreateRequest
{
    public required string Title { get; set; }
    public required string ChannelId { get; set; }
    public required string Content { get; set; }
}

