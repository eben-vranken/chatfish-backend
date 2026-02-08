using System.Text.Json.Serialization;

namespace BackEnd.DTOs;

public class PostRequest
{
    public string Title { get; set; }
    public string Context { get; set; }
    public string ChannelId { get; set; }
}