namespace BackEnd.DTOs;

public class UserTimeoutCreateRequest
{
    public string TargetUserId { get; set; } = null!;
    public int DurationMinutes { get; set; }
}
