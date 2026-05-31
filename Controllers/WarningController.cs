using BackEnd.DTOs;
using BackEnd.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using WebSocketManager = BackEnd.Services.WebSocketManager;

namespace BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WarningController(WarningService warningService, WebSocketManager webSocketManager) : ControllerBase
{
    /// <summary>
    /// Stuurt een waarschuwing naar een gebruiker. Alleen moderators en admins.
    /// De waarschuwing wordt gelogd in de database en de gebruiker wordt
    /// direct genotificeerd via WebSocket en/of browser-push.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<WarningResponse>> Create([FromBody] WarningCreateRequest request)
    {
        var userId = User.FindFirst("userid")?.Value;
        var role = User.FindFirst("role")?.Value?.ToLower();

        if (role != "moderator" && role != "admin")
            return Forbid();

        var warning = await warningService.CreateWarning(request.TargetUserId, userId!, request.Reason);
        return Ok(warning);
    }

    /// <summary>
    /// Geeft alle waarschuwingen terug die aan de ingelogde gebruiker gericht zijn.
    /// Gebruikt door de frontend om gemiste waarschuwingen te tonen na inloggen.
    /// </summary>
    [HttpGet("my")]
    public async Task<ActionResult<List<WarningResponse>>> GetMy()
    {
        var userId = User.FindFirst("userid")?.Value;
        var warnings = await warningService.GetByTargetUser(userId!);
        return Ok(warnings);
    }

    /// <summary>
    /// WebSocket-eindpunt voor real-time waarschuwingsmeldingen.
    /// De browser verbindt hier na inloggen; het jwt-cookie wordt automatisch meegestuurd.
    /// </summary>
    [HttpGet("ws")]
    public async Task ConnectWebSocket()
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var userId = User.FindFirst("userid")?.Value;
        if (userId == null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        webSocketManager.AddConnection(userId, webSocket);

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
            webSocketManager.RemoveConnection(userId, webSocket);
            if (webSocket.State == WebSocketState.Open)
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Verbinding gesloten", CancellationToken.None);
        }
    }
}
