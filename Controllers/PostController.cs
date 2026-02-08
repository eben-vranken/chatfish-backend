using BackEnd.DTOs;
using BackEnd.Services;
using BackEnd.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PostController(PostService postService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Post>> Add(PostCreateRequest postCreateRequest)
    {
        var userId = User.FindFirst("userid")?.Value;
        var createdPost = await postService.Add(postCreateRequest, userId);
        return Ok(createdPost);
    }
    
    [HttpGet]
    public async Task<List<PostResponse>> GetAll() =>
        await postService.GetAll();
    
    [HttpGet("{postId:length(24)}")]
    public async Task<ActionResult<PostResponse>> Get(string postId)
    {
        var userId = User.FindFirst("userid")?.Value;
        var post = await postService.Get(postId, userId);
        return post is null ? NotFound() : Ok(post);
    }
    
    [HttpGet("channel/{channelId:length(24)}")]
    public async Task<ActionResult<List<PostResponse>>> GetByChannelId(string channelId)
    {
        var userId = User.FindFirst("userid")?.Value;
        var posts = await postService.GetByChannelId(channelId, userId);
        return Ok(posts);
    }

    [HttpDelete("{postId:length(24)}")]
    public async Task<ActionResult> DeleteById(string postId)
    {
        var userId = User.FindFirst("userid")?.Value;
        var deleted = await postService.Delete(postId, userId);

        if (!deleted)
            return NotFound();

        return NoContent();
    }
    
    [HttpPut("update")]
    public async Task<ActionResult<PostResponse>> Update(PostUpdateRequest postUpdateRequest)
    {
        var userId = User.FindFirst("userid")?.Value;
        var updatedPost = await postService.Update(userId, postUpdateRequest);
        return Ok(updatedPost);
    }

    
}