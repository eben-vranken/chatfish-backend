namespace BackEnd.DTOs;

public class ChannelCreateRequest
{
    public required string ChannelName { get; set; }
    public required string ChannelDescription { get; set; }
    public required string ScenarioId { get; set; }
}
