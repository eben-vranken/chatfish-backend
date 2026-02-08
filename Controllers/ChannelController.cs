using BackEnd.DTOs;
using BackEnd.Models;
using Microsoft.AspNetCore.Mvc;
using BackEnd.Services;

using Microsoft.AspNetCore.Authorization;

namespace BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChannelController(ChannelService channelService) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<Channel>> Add(ChannelCreateRequest channelCreateRequest)
    {
        var newChannel = await channelService.Add(channelCreateRequest);
        return CreatedAtAction(nameof(GetById), new { id = newChannel.ChannelId }, newChannel);
    }

    [HttpGet]
    public async Task<ActionResult<List<Channel>>> GetAll() =>
        Ok(await channelService.GetAll());

    [HttpGet("{id:length(24)}")]
    public async Task<ActionResult<Channel>> GetById(string id)
    {
        var channel = await channelService.Get(id);
        return channel is null ? NotFound() : Ok(channel);
    }
    [HttpGet("scenario/{id:length(24)}")]
    public async Task<ActionResult<List<Channel>>> GetByScenarioId(string id)
    {
        var wantedchannels = await channelService.GetChannelsByScenarioId(id);
        return wantedchannels is null ? NotFound() : Ok(wantedchannels);
    }
    [HttpDelete("{id:length(24)}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(string id)
    {
        var channel = channelService.Get(id);
        if (channel is null)
        {
            return NotFound();
        }
        await channelService.Delete(id);
        return NoContent();
    }
    [HttpPut("{id:length(24)}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<Channel>> Update(string id, [FromForm] ChannelUpdateRequest channelUpdateRequest)
    {
        var channel = await channelService.Get(id);
        if (channel is null)
        {
            return NotFound();
        }
        channel.ChannelName = channelUpdateRequest.ChannelName;
        channel.ChannelDescription = channelUpdateRequest.ChannelDescription;
        await channelService.Update(id, channel);
        return Ok(channel);
    }
}
