using Microsoft.AspNetCore.Http;

namespace BackEnd.DTOs;

public class ChatUpdateRequest
{
    public string Name { get; set; } = string.Empty;

    public IFormFile? ProfilePicture { get; set; }
}

