using Microsoft.AspNetCore.Mvc;
using BackEnd.Services;
using Microsoft.AspNetCore.Authorization;

namespace BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PushSubscriptionController(PushNotificationService pushNotificationService) : ControllerBase
{
    [HttpGet("vapid-public-key")]
    [AllowAnonymous]
    public ActionResult<string> GetVapidPublicKey()
    {
        return Ok(new { publicKey = pushNotificationService.GetPublicKey() });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
    {
        var userId = User.FindFirst("userid")?.Value;
        
        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User ID not found in token");

        await pushNotificationService.Subscribe(userId, request.Endpoint, request.P256dh, request.Auth);
        return Ok(new { message = "Subscribed successfully" });
    }

    [HttpDelete]
    [Authorize]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest request)
    {
        await pushNotificationService.Unsubscribe(request.Endpoint);
        return Ok(new { message = "Unsubscribed successfully" });
    }
}

public record SubscribeRequest(string Endpoint, string P256dh, string Auth);
public record UnsubscribeRequest(string Endpoint);
