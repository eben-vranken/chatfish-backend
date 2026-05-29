using BackEnd.DTOs;
using BackEnd.Models;
using BackEnd.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TicketController(TicketService ticketService) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<List<Ticket>>> GetMine()
    {
        var userId = User.FindFirst("userid")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        return Ok(await ticketService.GetByUser(userId));
    }

    [HttpGet("mine/{scenarioId:length(24)}")]
    public async Task<ActionResult<bool>> HasTicket(string scenarioId)
    {
        var userId = User.FindFirst("userid")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        return Ok(await ticketService.HasTicket(userId, scenarioId));
    }

    [HttpPost]
    public async Task<ActionResult<Ticket>> Buy(TicketCreateRequest request)
    {
        var userId = User.FindFirst("userid")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        var ticket = await ticketService.Buy(userId, request.ScenarioId);
        return Ok(ticket);
    }
}
