using BackEnd.DTOs;
using BackEnd.Models;
using BackEnd.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CommentController(CommentService commentService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Comment>> Add(CommentCreateRequest commentCreateRequest)
    {
        var userId = User.Claims.FirstOrDefault(c => c.Type == "userid")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var newComment = await commentService.Add(commentCreateRequest, userId);
        return CreatedAtAction(nameof(Get), new { id = newComment.CommentId }, newComment);
    }

    [HttpGet("post/{postId:length(24)}")]
    public async Task<ActionResult<List<Comment>>> GetByPostId(string postId)
    {
        var comments = await commentService.GetByPostId(postId);
        return Ok(comments);
    }

    [HttpGet("{id:length(24)}")]
    public async Task<ActionResult<Comment>> Get(string id)
    {
        var comment = await commentService.Get(id);
        return comment is null ? NotFound() : Ok(comment);
    }

    [HttpDelete("{id:length(24)}")]
    public async Task<IActionResult> Delete(string id)
    {
        var userId = User.Claims.FirstOrDefault(c => c.Type == "userid")?.Value;
        var userRole = User.Claims.FirstOrDefault(c => c.Type == "role")?.Value;

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User not authenticated");

        var comment = await commentService.Get(id);
        if (comment is null)
            return NotFound("Comment not found");

        // Check if user is the author OR is an admin
        var isOwner = comment.AuthorId == userId;
        var isAdmin = userRole?.Equals("admin", StringComparison.OrdinalIgnoreCase) == true;

        if (!isOwner && !isAdmin)
            return Forbid("You can only delete your own comments");

        await commentService.Delete(id);
        return NoContent();
    }

    [HttpPut]
    public async Task<IActionResult> Edit(CommentUpdateRequest commentUpdateRequest)
    {
        var userId = User.Claims.FirstOrDefault(c => c.Type == "userid")?.Value;

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User not authenticated");

        var comment = await commentService.Get(commentUpdateRequest.CommentId);
        
        if (comment is null)
            return NotFound("Comment not found");

        // Only the owner can edit their comment (not even admins)
        if (comment.AuthorId != userId)
            return Forbid("You can only edit your own comments");

        comment.Content = commentUpdateRequest.Content;
        comment.UpdatedAt = commentUpdateRequest.UpdatedAt;
        
        await commentService.Edit(comment);
        return Ok(comment);
    }
}