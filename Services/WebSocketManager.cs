using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace BackEnd.Services;

public class WebSocketManager
{
    private readonly List<WebSocket> _connections = [];
    private readonly Lock _lock = new();

    public void AddConnection(WebSocket webSocket)
    {
        lock (_lock)
        {
            _connections.Add(webSocket);
        }
    }

    public void RemoveConnection(WebSocket webSocket)
    {
        lock (_lock)
        {
            _connections.Remove(webSocket);
        }
    }

    public async Task BroadcastToAllAsync<T>(T message)
    {
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        var buffer = new ArraySegment<byte>(bytes);

        List<WebSocket> connectionsToRemove = [];
        List<WebSocket> connectionsToBroadcast;

        // Copy connections list to avoid locking during async operations
        lock (_lock)
        {
            connectionsToBroadcast = [.. _connections];
        }

        // Broadcast to all connections
        foreach (var connection in connectionsToBroadcast)
        {
            if (connection.State == WebSocketState.Open)
            {
                try
                {
                    await connection.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch
                {
                    // Connection is broken, mark for removal
                    connectionsToRemove.Add(connection);
                }
            }
            else
            {
                // Connection is not open, mark for removal
                connectionsToRemove.Add(connection);
            }
        }

        // Remove broken connections
        if (connectionsToRemove.Count > 0)
        {
            lock (_lock)
            {
                foreach (var connection in connectionsToRemove)
                {
                    _connections.Remove(connection);
                }
            }
        }
    }

    public int GetConnectionCount()
    {
        lock (_lock)
        {
            return _connections.Count;
        }
    }
}

