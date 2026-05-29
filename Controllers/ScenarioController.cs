using BackEnd.DTOs;
using BackEnd.Models;
using BackEnd.Services;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authorization;

namespace BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ScenarioController(ScenarioService scenarioService) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<Scenario>> Add(ScenarioCreateRequest scenarioCreateRequest)
    {
        var userId = User.FindFirst("userid")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        var newScenario = await scenarioService.Add(new ScenarioCreateRequest
        {
            Name = scenarioCreateRequest.Name,
            Description = scenarioCreateRequest.Description,
            CreatedBy = userId,
            StartMoment = scenarioCreateRequest.StartMoment,
            DurationMinutes = scenarioCreateRequest.DurationMinutes,
            Price = scenarioCreateRequest.Price,
            SaleStatus = scenarioCreateRequest.SaleStatus
        });
        return CreatedAtAction(nameof(GetById), new { id = newScenario.ScenarioId }, newScenario);
    }

    [HttpGet]
    public async Task<ActionResult<List<Scenario>>> GetAll() =>
        Ok(await scenarioService.GetAll());

    [HttpGet("{id:length(24)}")]
    public async Task<ActionResult<Scenario>> GetById(string id)
    {
        var scenario = await scenarioService.GetById(id);
        return scenario is null ? NotFound() : Ok(scenario);
    }

    [HttpPut("{id:length(24)}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<Scenario>> Update(string id, Scenario updatedScenario)
    {
        var result = await scenarioService.Update(id, updatedScenario);

        if (!result)
            return NotFound($"Scenario met id {id} niet gevonden.");

        return Ok(updatedScenario);
    }

    [HttpPut("shift")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<bool>> ShiftScenario(ShiftScenarioRequest shiftScenario)
    {
        var success = await scenarioService.ShiftScenario(shiftScenario.ScenarioId, shiftScenario.NewFirstMessageUtc);
        return success ? Ok(true) : NotFound();
    }

    [HttpDelete("{id:length(24)}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<bool>> DeleteScenario(string id)
    {
        var deleted = await scenarioService.DeleteScenarioById(id);

        if (!deleted)
            return NotFound($"Scenario with id {id} not found.");

        return Ok(true);
    }
}
