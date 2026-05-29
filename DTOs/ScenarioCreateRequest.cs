namespace BackEnd.DTOs;

public class ScenarioCreateRequest
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public string? CreatedBy { get; set; }

    public DateTime? StartMoment { get; set; }
    public int? DurationMinutes { get; set; }
    public double? Price { get; set; }
    public string? SaleStatus { get; set; }
}

