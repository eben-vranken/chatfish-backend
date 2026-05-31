using BackEnd.DTOs;
using BackEnd.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BanController(BanService banService) : ControllerBase
{
    /// <summary>
    /// Legt een foyer-ban op voor een gebruiker. Scope: foyer-breed.
    /// Moderators en admins mogen bannen; een eventuele bestaande actieve ban wordt vervangen.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<BanResponse>> Create([FromBody] BanCreateRequest request)
    {
        if (!User.IsInRole("moderator") && !User.IsInRole("admin"))
            return Forbid();

        var issuedById = User.FindFirst("userid")?.Value!;
        var result = await banService.Create(request.TargetUserId, issuedById, request.Reason);
        return Ok(result);
    }

    /// <summary>
    /// Geeft de actieve ban van de ingelogde gebruiker terug (204 als er geen is).
    /// Gebruikt door de foyerGuard om toegang te blokkeren.
    /// </summary>
    [HttpGet("my")]
    public async Task<ActionResult<BanResponse>> GetMy()
    {
        var userId = User.FindFirst("userid")?.Value!;
        var ban = await banService.GetActiveResponse(userId);
        return ban == null ? NoContent() : Ok(ban);
    }

    /// <summary>
    /// Lijst van alle actieve bans. Alleen voor admins — gebruikt door de moderator-view
    /// om per auteur te tonen of die gebanned is.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<BanResponse>>> GetAll()
    {
        if (!User.IsInRole("admin"))
            return Forbid();

        var bans = await banService.GetAllActive();
        return Ok(bans);
    }

    /// <summary>
    /// Heft de actieve ban van een gebruiker op. Alleen admins.
    /// </summary>
    [HttpDelete("{targetUserId}")]
    public async Task<ActionResult> Lift(string targetUserId)
    {
        if (!User.IsInRole("admin"))
            return Forbid();

        var liftedById = User.FindFirst("userid")?.Value!;
        await banService.Lift(targetUserId, liftedById);
        return NoContent();
    }
}
