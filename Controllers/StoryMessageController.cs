using BackEnd.DTOs;
using Microsoft.AspNetCore.Mvc;
using BackEnd.Services;
using BackEnd.Models;
using System.Net.WebSockets;
using WebSocketManager = BackEnd.Services.WebSocketManager;

using Microsoft.AspNetCore.Authorization;
using Minio;
using BackEnd.Util;
using System.Security.Cryptography;

namespace BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StoryMessageController(
    StoryMessageService storyMessageService,
    WebSocketManager webSocketManager,
    IMinioClient minioClient,
    ILogger<StoryMessageController> logger
) : ControllerBase
{
    private const string BUCKET = "storymessages";
    private async Task PopulateFileUrlAsync(StoryMessage msg)
    {
        if (msg?.FileContent is null || msg.FileContent.Length == 0)
            return;

        try
        {
            msg.FileContent = await MinioUtils.GetObjectUrlAsync(
                minioClient,
                BUCKET,
                msg.FileContent
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed generating file URL for StoryMessage {Id}", msg.StoryMessageId);
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<StoryMessage>>> GetAll()
    {
        var storyMessages = await storyMessageService.GetAll();

        foreach (var msg in storyMessages)
            await PopulateFileUrlAsync(msg);

        return Ok(storyMessages);
    }

    [HttpGet("{id:length(24)}")]
    public async Task<ActionResult<StoryMessage>> GetById(string id)
    {
        var msg = await storyMessageService.Get(id);

        if (msg is null)
            return NotFound();

        await PopulateFileUrlAsync(msg);

        return Ok(msg);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<StoryMessage>> Add([FromBody] StoryMessageCreateRequest req)
    {
        var created = await storyMessageService.Add(req);
        return CreatedAtAction(nameof(GetById), new { id = created.StoryMessageId }, created);
    }

    [HttpPut("{id:length(24)}")]
    [Authorize(Policy = "AdminOnly")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<StoryMessage>> Update(
        string id,
        [FromForm] StoryMessageUpdateRequest request
    )
    {
        var message = await storyMessageService.Get(id);
        if (message is null)
            return NotFound();

        message.TextContent = request.TextContent;
        message.ChatId = request.ChatId;
        message.CharacterId = request.CharacterId;
        message.PlannedAt = request.PlannedAt;
        message.UpdatedAt = DateTime.UtcNow;

        // Handle optional file upload
        if (request.FileAttachment is { Length: > 0 })
        {
            try
            {
                await using var memoryStream = new MemoryStream();
                await request.FileAttachment.CopyToAsync(memoryStream);

                var hashBytes = SHA256.HashData(memoryStream.ToArray());
                var fileHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

                var contentType =
                    string.IsNullOrWhiteSpace(request.FileAttachment.ContentType)
                        ? "application/octet-stream"
                        : request.FileAttachment.ContentType!;

                await MinioUtils.PutObjectAsync(minioClient, BUCKET, fileHash, memoryStream, contentType);

                message.FileContent = fileHash;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "File upload failed for StoryMessage {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "File upload failed.");
            }
        }

        await storyMessageService.Update(id, message);

        // Convert hash → URL for frontend
        await PopulateFileUrlAsync(message);

        return Ok(message);
    }

    [HttpDelete("{id:length(24)}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(string id)
    {
        await storyMessageService.Delete(id);
        return NoContent();
    }

    [HttpGet("chat/{chatId:length(24)}")]
    public async Task<ActionResult<List<StoryMessage>>> GetByChatId(string chatId)
    {
        var messages = await storyMessageService.GetByChatId(chatId);

        foreach (var msg in messages)
            await PopulateFileUrlAsync(msg);

        return Ok(messages);
    }

    [HttpGet("chat/{chatId:length(24)}/admin")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<List<StoryMessage>>> GetByChatIdAdmin(string chatId)
    {
        var messages = await storyMessageService.GetByChatId(chatId);

        foreach (var msg in messages)
            await PopulateFileUrlAsync(msg);

        return Ok(messages);
    }
    
    [HttpGet("chat/{chatId:length(24)}/since")]
    public async Task<ActionResult<List<StoryMessage>>> GetByChatIdSince(string chatId, [FromQuery] string? since)
    {
        if (string.IsNullOrEmpty(since))
        {
            var allMessages = await storyMessageService.GetByChatId(chatId);
            return Ok(allMessages);
        }

        if (!DateTime.TryParse(since, out var sinceDate))
        {
            return BadRequest("Invalid date format");
        }

        var messages = await storyMessageService.GetByChatIdSince(chatId, sinceDate);
        return Ok(messages);
    }

    [HttpGet("ws")]
    public async Task WebSocket()
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        webSocketManager.AddConnection(webSocket);

        try
        {
            var buffer = new byte[1024 * 4];
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) break;
            }
        }
        finally
        {
            webSocketManager.RemoveConnection(webSocket);
            if (webSocket.State == WebSocketState.Open)
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
        }
    }
}
