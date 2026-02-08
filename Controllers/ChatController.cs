using BackEnd.DTOs;
using BackEnd.Services;
using BackEnd.Models;
using BackEnd.Util;
using dotenv.net.Utilities;
using Microsoft.AspNetCore.Mvc;
using Minio;

using Microsoft.AspNetCore.Authorization;

namespace BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly ChatService _chatsService;
    private readonly IMinioClient _minioClient;
    private readonly ILogger<ChatController> _logger;
    private readonly string _bucketName;

    public ChatController(ChatService chatsService, IMinioClient minioClient, ILogger<ChatController> logger)
    {
        _chatsService = chatsService;
        _minioClient = minioClient;
        _logger = logger;
        _bucketName = EnvReader.TryGetStringValue("MINIO_BUCKET", out var bucket) && !string.IsNullOrWhiteSpace(bucket)
            ? bucket
            : "chats";
    }

    [HttpGet]
    public async Task<ActionResult<List<Chat>>> GetAll()
    {
        var chats = await _chatsService.GetAll();
        await PopulateProfilePictureUrlsAsync(chats);
        return Ok(chats);
    }

    [HttpGet("{id:length(24)}")]
    public async Task<ActionResult<Chat>> GetById(string id)
    {
        var chat = await _chatsService.Get(id);

        if (chat is null)
            return NotFound();
        await PopulateProfilePictureUrlAsync(chat);
        return Ok(chat);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)] 
    public async Task<ActionResult<Chat>> Add([FromForm] ChatCreateRequest chatCreateRequest)
    {
        string? profilePictureData;
        try
        {
            profilePictureData = await _chatsService.UploadProfilePictureAsync(chatCreateRequest.ProfilePicture);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload profile picture for chat {ChatName}", chatCreateRequest.Name);
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to upload profile picture.");
        }

        var newChat = new Chat(chatCreateRequest.Name, chatCreateRequest.ScenarioId, profilePictureData);

        await _chatsService.Add(newChat);

        return CreatedAtAction(nameof(GetById), new { id = newChat.ChatId }, newChat);
    }

    [HttpPut("{id:length(24)}")]
    [Authorize(Policy = "AdminOnly")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)] 
    public async Task<ActionResult<Chat>> Update(string id, [FromForm] ChatUpdateRequest chatUpdateRequest)
    {
        var chat = await _chatsService.Get(id);

        if (chat is null)
            return NotFound();

        // Update name
        chat.Name = chatUpdateRequest.Name;

        // Handle profile picture upload if provided
        if (chatUpdateRequest.ProfilePicture is { Length: > 0 })
        {
            try
            {
                chat.ProfilePicture = await _chatsService.UploadProfilePictureAsync(chatUpdateRequest.ProfilePicture);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload profile picture for chat {ChatId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to upload profile picture.");
            }
        }

        await _chatsService.Update(id, chat);

        await PopulateProfilePictureUrlAsync(chat);

        return Ok(chat);
    }

    [HttpDelete("{id:length(24)}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(string id)
    {
        var chat = await _chatsService.Get(id);

        if (chat is null)
            return NotFound();

        await _chatsService.Delete(id);

        await _chatsService.DeleteProfilePictureIfUnusedAsync(chat.ProfilePicture, id);

        return NoContent();
    }

    [HttpGet("scenario/{scenarioId:length(24)}")]
    public async Task<ActionResult<List<Chat>>> GetByScenario(string scenarioId)
    {
        var chats = await _chatsService.GetAll();
        var scenarioChats = chats.Where(c => c.ScenarioId == scenarioId).ToList();

        if (scenarioChats.Count == 0)
            return NotFound($"No chats found for scenario {scenarioId}");

        await PopulateProfilePictureUrlsAsync(scenarioChats);

        return Ok(scenarioChats);
    }

    private async Task PopulateProfilePictureUrlsAsync(IEnumerable<Chat> chats)
    {
        var tasks = chats.Select(PopulateProfilePictureUrlAsync);
        await Task.WhenAll(tasks);
    }

    private async Task PopulateProfilePictureUrlAsync(Chat chat)
    {
        if (chat?.ProfilePicture is null or { Length: 0 })
            return;

        try
        {
            chat.ProfilePicture = await MinioUtils.GetObjectUrlAsync(_minioClient, _bucketName, chat.ProfilePicture);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate profile picture URL for chat {ChatId}", chat.ChatId ?? chat.Name);
        }
    }
}