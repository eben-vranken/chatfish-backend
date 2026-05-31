namespace BackEnd.DTOs;

public class UserTimeoutResponse
{
    public string TimeoutId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string IssuedById { get; set; } = null!;
    public string IssuedByUsername { get; set; } = null!;
    public DateTime EndsAt { get; set; }
    public DateTime IssuedAt { get; set; }
}
