namespace BackEnd.DTOs;

public class ShiftScenarioRequest
{
    public required string ScenarioId { get; set; }
    public required DateTime NewFirstMessageUtc { get; set; }
}