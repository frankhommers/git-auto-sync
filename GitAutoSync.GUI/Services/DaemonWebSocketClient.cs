using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace GitAutoSync.GUI.Services;

public class DaemonWebSocketClient : IDisposable
{
  private ClientWebSocket? _webSocket;
  private CancellationTokenSource? _cancellationTokenSource;
  private const string WebSocketUrl = "ws://127.0.0.1:52847/ws";

  public event EventHandler<DaemonEventArgs>? EventReceived;

  public async Task ConnectAsync()
  {
    _webSocket = new ClientWebSocket();
    _cancellationTokenSource = new CancellationTokenSource();

    await _webSocket.ConnectAsync(new Uri(WebSocketUrl), _cancellationTokenSource.Token);

    _ = Task.Run(() => ReceiveLoop(_cancellationTokenSource.Token));
  }

  private async Task ReceiveLoop(CancellationToken cancellationToken)
  {
    byte[] buffer = new byte[1024 * 4];

    try
    {
      while (_webSocket?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
      {
        WebSocketReceiveResult result = await _webSocket.ReceiveAsync(
          new ArraySegment<byte>(buffer),
          cancellationToken);

        if (result.MessageType == WebSocketMessageType.Close)
        {
          await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
          break;
        }

        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
        DaemonEvent? daemonEvent = JsonSerializer.Deserialize(message, AppJsonContext.Default.DaemonEvent);

        if (daemonEvent != null)
        {
          EventReceived?.Invoke(this, new DaemonEventArgs(daemonEvent));
        }
      }
    }
    catch (OperationCanceledException)
    {
      // Normal shutdown
    }
    catch (Exception ex)
    {
      Console.WriteLine($"WebSocket error: {ex.Message}");
    }
  }

  public async Task DisconnectAsync()
  {
    if (_webSocket?.State == WebSocketState.Open)
    {
      _cancellationTokenSource?.Cancel();
      await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
    }
  }

  public void Dispose()
  {
    _cancellationTokenSource?.Cancel();
    _webSocket?.Dispose();
    _cancellationTokenSource?.Dispose();
  }
}

public record DaemonEvent(string Type, DateTime Timestamp, JsonElement Data);

public class DaemonEventArgs : EventArgs
{
  public DaemonEvent Event { get; }

  public DaemonEventArgs(DaemonEvent ev)
  {
    Event = ev;
  }
}