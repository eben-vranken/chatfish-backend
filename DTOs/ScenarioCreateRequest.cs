namespace BackEnd.DTOs;

public class ScenarioCreateRequest
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public string? CreatedBy { get; set; }
}

