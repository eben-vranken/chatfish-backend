namespace BackEnd.DTOs;

public class ChannelUpdateRequest
{
    public required string ChannelName { get; set; }
    public required string ChannelDescription { get; set; }
}