namespace BackEnd.DTOs;

public class PostUpdateRequest
{
    public required string PostId { get; set; }
    public required string AuthorId { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }
}