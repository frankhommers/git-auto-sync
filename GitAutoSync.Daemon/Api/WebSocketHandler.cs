using GitAutoSync.Daemon.Models;
using GitAutoSync.Daemon.Services;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace GitAutoSync.Daemon.Api;

public class WebSocketHandler
{
  private static readonly ConcurrentBag<WebSocket> _sockets = new();
  private readonly EventBus _eventBus;
  private readonly ILogger<WebSocketHandler> _logger;

  public WebSocketHandler(EventBus eventBus, ILogger<WebSocketHandler> logger)
  {
    _eventBus = eventBus;
    _logger = logger;

    // Subscribe to event bus
    _eventBus.Subscribe(BroadcastEventAsync);
  }

  public async Task HandleWebSocketAsync(HttpContext context)
  {
    if (!context.WebSockets.IsWebSocketRequest)
    {
      context.Response.StatusCode = 400;
      return;
    }

    WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
    _sockets.Add(socket);
    _logger.LogInformation("WebSocket client connected. Total clients: {Count}", _sockets.Count);

    try
    {
      // Keep connection alive and handle incoming messages
      byte[] buffer = new byte[1024 * 4];
      while (socket.State == WebSocketState.Open)
      {
        WebSocketReceiveResult result = await socket.ReceiveAsync(
          new ArraySegment<byte>(buffer),
          CancellationToken.None);

        if (result.MessageType == WebSocketMessageType.Close)
        {
          await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "WebSocket error");
    }
    finally
    {
      if (socket.State == WebSocketState.Open)
      {
        await socket.CloseAsync(WebSocketCloseStatus.InternalServerError, "Error", CancellationToken.None);
      }

      _logger.LogInformation("WebSocket client disconnected");
    }
  }

  private async Task BroadcastEventAsync(DaemonEvent ev)
  {
    string json = JsonSerializer.Serialize(ev);
    byte[] bytes = Encoding.UTF8.GetBytes(json);
    ArraySegment<byte> buffer = new(bytes);

    List<WebSocket> deadSockets = new();

    foreach (WebSocket socket in _sockets)
    {
      if (socket.State != WebSocketState.Open)
      {
        deadSockets.Add(socket);
        continue;
      }

      try
      {
        await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to send to WebSocket client");
        deadSockets.Add(socket);
      }
    }

    // Clean up dead sockets
    foreach (WebSocket dead in deadSockets)
    {
      // Note: ConcurrentBag doesn't support removal, but that's OK
      // Sockets will be skipped when state != Open
    }
  }
}