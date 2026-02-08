namespace BackEnd.DTOs;

public class PostResponse
{
    public string PostId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public string AuthorId { get; set; }
    public string? AuthorUsername { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string ChannelId { get; set; }
    public bool IsArchived { get; set; }
    public bool IsEditable { get; set; }
    public bool IsDeletable { get; set; }
}