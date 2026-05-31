using BackEnd.DTOs;
using BackEnd.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserTimeoutController(UserTimeoutService userTimeoutService) : ControllerBase
{
    /// <summary>
    /// Geeft een time-out aan een gebruiker. Alleen moderators en admins.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<UserTimeoutResponse>> Create([FromBody] UserTimeoutCreateRequest request)
    {
        if (!User.IsInRole("moderator") && !User.IsInRole("admin"))
            return Forbid();

        if (request.DurationMinutes <= 0)
            return BadRequest("Duur moet positief zijn.");

        var userId = User.FindFirst("userid")?.Value!;
        var result = await userTimeoutService.Create(request.TargetUserId, userId, request.DurationMinutes);
        return Ok(result);
    }

    /// <summary>
    /// Geeft de actieve time-out van de ingelogde gebruiker terug, of 204 als er geen is.
    /// </summary>
    [HttpGet("my")]
    public async Task<ActionResult<UserTimeoutResponse>> GetMy()
    {
        var userId = User.FindFirst("userid")?.Value!;
        var timeout = await userTimeoutService.GetActiveResponse(userId);
        return timeout == null ? NoContent() : Ok(timeout);
    }

    /// <summary>
    /// Heft de actieve time-out van een gebruiker op. Alleen moderators en admins.
    /// </summary>
    [HttpDelete("{targetUserId}")]
    public async Task<ActionResult> Lift(string targetUserId)
    {
        if (!User.IsInRole("moderator") && !User.IsInRole("admin"))
            return Forbid();

        await userTimeoutService.Lift(targetUserId);
        return NoContent();
    }
}
