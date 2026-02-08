using Microsoft.AspNetCore.Http;

namespace BackEnd.DTOs;

public class ChatCreateRequest
{
    public string Name { get; set; } = string.Empty;

    public string ScenarioId { get; set; } = string.Empty;

    public IFormFile? ProfilePicture { get; set; }
}

