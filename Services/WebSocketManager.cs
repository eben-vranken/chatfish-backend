using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace BackEnd.Services;

public class WebSocketManager
{
    // Broadcast connections (StoryMessage ws)
    private readonly List<WebSocket> _connections = [];

    // Per-user connections (Warning ws)
    private readonly Dictionary<string, List<WebSocket>> _userConnections = new();

    private readonly Lock _lock = new();

    // ── Broadcast API (unchanged) ──────────────────────────────────────────

    public void AddConnection(WebSocket webSocket)
    {
        lock (_lock) { _connections.Add(webSocket); }
    }

    public void RemoveConnection(WebSocket webSocket)
    {
        lock (_lock) { _connections.Remove(webSocket); }
    }

    public async Task BroadcastToAllAsync<T>(T message)
    {
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        var buffer = new ArraySegment<byte>(bytes);

        List<WebSocket> connectionsToRemove = [];
        List<WebSocket> connectionsToBroadcast;

        lock (_lock) { connectionsToBroadcast = [.. _connections]; }

        foreach (var connection in connectionsToBroadcast)
        {
            if (connection.State == WebSocketState.Open)
            {
                try { await connection.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None); }
                catch { connectionsToRemove.Add(connection); }
            }
            else { connectionsToRemove.Add(connection); }
        }

        if (connectionsToRemove.Count > 0)
        {
            lock (_lock)
            {
                foreach (var c in connectionsToRemove) _connections.Remove(c);
            }
        }
    }

    public int GetConnectionCount()
    {
        lock (_lock) { return _connections.Count; }
    }

    // ── Per-user API (Warning notifications) ──────────────────────────────

    public void AddConnection(string userId, WebSocket webSocket)
    {
        lock (_lock)
        {
            if (!_userConnections.TryGetValue(userId, out var list))
            {
                list = [];
                _userConnections[userId] = list;
            }
            list.Add(webSocket);
        }
    }

    public void RemoveConnection(string userId, WebSocket webSocket)
    {
        lock (_lock)
        {
            if (!_userConnections.TryGetValue(userId, out var list)) return;
            list.Remove(webSocket);
            if (list.Count == 0) _userConnections.Remove(userId);
        }
    }

    public async Task SendToUserAsync<T>(string userId, T message)
    {
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        var buffer = new ArraySegment<byte>(bytes);

        List<WebSocket> connections;
        lock (_lock)
        {
            if (!_userConnections.TryGetValue(userId, out var list)) return;
            connections = [.. list];
        }

        List<WebSocket> toRemove = [];
        foreach (var ws in connections)
        {
            if (ws.State == WebSocketState.Open)
            {
                try { await ws.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None); }
                catch { toRemove.Add(ws); }
            }
            else { toRemove.Add(ws); }
        }

        if (toRemove.Count > 0)
        {
            lock (_lock)
            {
                if (!_userConnections.TryGetValue(userId, out var list)) return;
                foreach (var ws in toRemove) list.Remove(ws);
                if (list.Count == 0) _userConnections.Remove(userId);
            }
        }
    }
}
