namespace BackEnd.DTOs;

public class WarningCreateRequest
{
    public string TargetUserId { get; set; } = null!;
    public string Reason { get; set; } = null!;
}
