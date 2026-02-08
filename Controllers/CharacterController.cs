using BackEnd.DTOs;
using BackEnd.Models;
using Microsoft.AspNetCore.Mvc;
using BackEnd.Services;
using BackEnd.Util;
using dotenv.net.Utilities;
using Microsoft.AspNetCore.Authorization;
using Minio;
using System.Security.Cryptography;

namespace BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CharacterController : ControllerBase
{
    private readonly CharacterService _characterService;
    private readonly IMinioClient _minioClient;
    private readonly ILogger<CharacterController> _logger;
    private readonly string _bucketName;

    public CharacterController(CharacterService characterService, IMinioClient minioClient, ILogger<CharacterController> logger)
    {
        _characterService = characterService;
        _minioClient = minioClient;
        _logger = logger;
        _bucketName = "characters";
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)] 
    public async Task<ActionResult<Character>> Add([FromForm] CharacterCreateRequest newCharacter)
    {
        string? profilePictureData = null;

        if (newCharacter.ProfilePicture is { Length: > 0 })
        {
            try
            {
                await using var memoryStream = new MemoryStream();
                await newCharacter.ProfilePicture.CopyToAsync(memoryStream);
                var hashBytes = SHA256.HashData(memoryStream.ToArray());
                var fileHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

                var contentType = string.IsNullOrWhiteSpace(newCharacter.ProfilePicture.ContentType)
                    ? "application/octet-stream"
                    : newCharacter.ProfilePicture.ContentType!;

                await MinioUtils.PutObjectAsync(_minioClient, _bucketName, fileHash, memoryStream, contentType);
                profilePictureData = fileHash;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload profile picture for character {CharacterName}", newCharacter.Name);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to upload profile picture.");
            }
        }

        var createdCharacter = await _characterService.Add(newCharacter, profilePictureData);
        await PopulateProfilePictureUrlAsync(createdCharacter);
        return CreatedAtAction(nameof(GetById), new { id = createdCharacter.CharacterId }, createdCharacter);
    }

    [HttpDelete("{id:length(24)}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(string id)
    {
        var character = await _characterService.GetById(id);

        if (character is null)
            return NotFound($"Character with id {id} not found");

        await _characterService.Delete(id);

        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<List<Character>>> GetAll()
    {
        var characters = await _characterService.GetAll();
        await PopulateProfilePictureUrlsAsync(characters);
        return Ok(characters);
    }
    
    [HttpGet("scenario/{id:length(24)}")]
    public async Task<ActionResult<List<Character>>> GetByScenario(string id)
    {
        var characters = await _characterService.GetByScenario(id);
        await PopulateProfilePictureUrlsAsync(characters);
        return Ok(characters);
    }

    [HttpGet("{id:length(24)}")]
    public async Task<ActionResult<Character>> GetById(string id)
    {
        var character = await _characterService.GetById(id);
        if (character is null)
            return NotFound();
        
        await PopulateProfilePictureUrlAsync(character);
        return Ok(character);
    }

    [HttpPut("{id:length(24)}")]
    [Authorize(Policy = "AdminOnly")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)] 
    public async Task<ActionResult<Character>> Update(string id, [FromForm] CharacterUpdateRequest characterUpdateRequest)
    {
        var character = await _characterService.GetById(id);

        if (character is null)
            return NotFound();

        // Update name
        character.Name = characterUpdateRequest.Name;

        // Handle profile picture upload if provided
        if (characterUpdateRequest.ProfilePicture is { Length: > 0 })
        {
            try
            {
                await using var memoryStream = new MemoryStream();
                await characterUpdateRequest.ProfilePicture.CopyToAsync(memoryStream);
                var fileBytes = memoryStream.ToArray();
                var hashBytes = SHA256.HashData(fileBytes);
                var fileHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

                var contentType = string.IsNullOrWhiteSpace(characterUpdateRequest.ProfilePicture.ContentType)
                    ? "application/octet-stream"
                    : characterUpdateRequest.ProfilePicture.ContentType!;

                await using var uploadStream = new MemoryStream(fileBytes);
                await MinioUtils.PutObjectAsync(_minioClient, _bucketName, fileHash, uploadStream, contentType);
                character.ProfilePicture = fileHash;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload profile picture for character {CharacterId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to upload profile picture.");
            }
        }
        // If no new picture is uploaded, the old one is kept (character.ProfilePicture remains unchanged)

        await _characterService.Edit(character);

        await PopulateProfilePictureUrlAsync(character);

        return Ok(character);
    }

    private async Task PopulateProfilePictureUrlsAsync(IEnumerable<Character> characters)
    {
        var tasks = characters.Select(PopulateProfilePictureUrlAsync);
        await Task.WhenAll(tasks);
    }

    private async Task PopulateProfilePictureUrlAsync(Character character)
    {
        if (character?.ProfilePicture is null or { Length: 0 })
            return;

        try
        {
            character.ProfilePicture = await MinioUtils.GetObjectUrlAsync(_minioClient, _bucketName, character.ProfilePicture);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate profile picture URL for character {CharacterId}", character.CharacterId ?? character.Name);
        }
    }
}
