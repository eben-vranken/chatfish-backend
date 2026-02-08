using System.ComponentModel.DataAnnotations;

namespace BackEnd.DTOs;

public class CommentUpdateRequest
{
    [Required(ErrorMessage = "CommentId is required")]
    public string CommentId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Content is required")]
    [StringLength(2000, MinimumLength = 1, ErrorMessage = "Content must be between 1 and 2000 characters")]
    public string Content { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}