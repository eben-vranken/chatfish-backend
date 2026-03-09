using Microsoft.AspNetCore.Mvc;
using BackEnd.Services;
using BackEnd.DTOs;
using BackEnd.Models;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;

namespace BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController(IWebHostEnvironment environment, UserService userService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<List<User>>> GetAll() =>
        Ok(await userService.GetAll());
    
    [HttpGet("{id:length(24)}")]
    public async Task<ActionResult<User>> GetById(string id)
    {
        var user = await userService.GetById(id);
        return (user is null) ? NotFound() : Ok(user);
    }
    
    [HttpGet("email/{email}")]
    public async Task<ActionResult<User>> GetByEmail(string email)
    {
        var user = await userService.GetByEmail(email);
        return (user is null) ? NotFound() : Ok(user);
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<User>> Add(UserCreateRequest userCreateRequest)
    {
        var newUser = await userService.Add(userCreateRequest);
        return CreatedAtAction(nameof(GetById), new { id = newUser.UserId }, newUser);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Authenticate(UserLoginRequest loginRequest)
    {
        var user = await userService.GetByEmail(loginRequest.Email);
        if (user == null)
            return Unauthorized(new { message = "Combinatie email/wachtwoord niet gevonden" });
        
        var token = await userService.Login(loginRequest.Email, loginRequest.Password);

        if (token == null)
        {
            return Unauthorized(new { message = "Combinatie email/wachtwoord niet gevonden" });
        }
        
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // Sowieso verplicht bij SameSiteMode.None
            SameSite = environment.IsDevelopment() ? SameSiteMode.None : SameSiteMode.Strict, // Development: None zodat cookies ook worden meegestuurd wanneer de frontend op een andere (localhost) poort draait.
            // Expires = DateTimeOffset.UtcNow.AddHours(24), -> Geen expiration = session cookie
        };
        
        Response.Cookies.Append("jwt", token, cookieOptions);

        // Decode token to get expiry time from the JWT
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.ReadJwtToken(token);
        var expiresAt = jwtToken.ValidTo.ToUniversalTime();
        var expiresAtUnix = ((DateTimeOffset)expiresAt).ToUnixTimeMilliseconds();

        return Ok(new UserLoginResponse()
        {
            UserId = user.UserId,
            Email = user.Email,
            Username = user.Username,
            Role = user.Role,
            ExpiresAt = expiresAtUnix
        });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public IActionResult Logout()
    {
        // Use Clear-Site-Data header to clear all site data including cookies
        Response.Headers.Append("Clear-Site-Data", "*");
        
        return Ok(new { message = "Logged out successfully" });
    }
}
