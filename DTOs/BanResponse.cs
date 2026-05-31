namespace BackEnd.DTOs;

public class BanResponse
{
    public string BanId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string IssuedById { get; set; } = null!;
    public string IssuedByUsername { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public DateTime BannedAt { get; set; }
}
