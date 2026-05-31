namespace BackEnd.DTOs;

public class WarningResponse
{
    public string WarningId { get; set; } = null!;
    public string TargetUserId { get; set; } = null!;
    public string IssuedById { get; set; } = null!;
    public string IssuedByUsername { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public DateTime IssuedAt { get; set; }
}
