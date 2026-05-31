namespace BackEnd.DTOs;

public class BanCreateRequest
{
    public string TargetUserId { get; set; } = null!;
    public string Reason { get; set; } = null!;
}
