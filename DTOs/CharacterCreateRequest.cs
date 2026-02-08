using Microsoft.AspNetCore.Http;

namespace BackEnd.DTOs;

public class CharacterCreateRequest
{
    public required string Name { get; set; }
    public IFormFile? ProfilePicture { get; set; }
    public required string ScenarioId { get; set; }
}

