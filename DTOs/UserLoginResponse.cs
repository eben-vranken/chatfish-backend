namespace BackEnd.DTOs;

public class UserLoginResponse
{
    public string UserId { get; set; }
    public string Email { get; set; }
    public string Username { get; set; } 
    public string Role { get; set; }
    public long ExpiresAt { get; set; } // Unix timestamp in milliseconds
}
