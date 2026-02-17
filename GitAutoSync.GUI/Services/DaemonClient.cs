using GitAutoSync.GUI.Models;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Serilog;

namespace GitAutoSync.GUI.Services;

// Shared options instance for AOT-safe serialization
internal static class JsonDefaults
{
  public static readonly JsonSerializerOptions Options = new(AppJsonContext.Default.Options);
}

public class DaemonClient
{
  private readonly HttpClient _httpClient;
  private const string BaseUrl = "http://127.0.0.1:52847";
  private const int MaxRetryAttempts = 3;

  public DaemonClient()
  {
    _httpClient = new HttpClient
    {
      BaseAddress = new Uri(BaseUrl),
      Timeout = TimeSpan.FromSeconds(30),
    };
  }

  public async Task<bool> IsHealthyAsync()
  {
    try
    {
      HttpResponseMessage response = await SendWithRetryAsync(
        () => new HttpRequestMessage(HttpMethod.Get, "/health"),
        "health check");
      return response.IsSuccessStatusCode;
    }
    catch
    {
      return false;
    }
  }

  public async Task<List<RepositoryDto>> GetRepositoriesAsync()
  {
    HttpResponseMessage response = await SendWithRetryAsync(
      () => new HttpRequestMessage(HttpMethod.Get, "/api/repositories"),
      "get repositories");
    await EnsureSuccessOrThrowAsync(response);
    return await response.Content.ReadFromJsonAsync(AppJsonContext.Default.ListRepositoryDto) ??
           new List<RepositoryDto>();
  }

  public async Task<RepositoryDto?> AddRepositoryAsync(string name, string path)
  {
    AddRepositoryRequest request = new(name, path);
    string json = JsonSerializer.Serialize(request, AppJsonContext.Default.AddRepositoryRequest);
    Log.Information("POST /api/repositories Body: {Json}", json);

    HttpResponseMessage response = await SendWithRetryAsync(
      () => new HttpRequestMessage(HttpMethod.Post, "/api/repositories")
      {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
      },
      "add repository");
    Log.Information("POST /api/repositories Response: {StatusCode}", (int) response.StatusCode);

    await EnsureSuccessOrThrowAsync(response);
    return await response.Content.ReadFromJsonAsync(AppJsonContext.Default.RepositoryDto);
  }

  public async Task RemoveRepositoryAsync(string id)
  {
    HttpResponseMessage response = await SendWithRetryAsync(
      () => new HttpRequestMessage(HttpMethod.Delete, $"/api/repositories/{id}"),
      "remove repository");
    await EnsureSuccessOrThrowAsync(response);
  }

  public async Task StartRepositoryAsync(string id)
  {
    HttpResponseMessage response = await SendWithRetryAsync(
      () => new HttpRequestMessage(HttpMethod.Post, $"/api/repositories/{id}/start"),
      "start repository");
    await EnsureSuccessOrThrowAsync(response);
  }

  public async Task StopRepositoryAsync(string id)
  {
    HttpResponseMessage response = await SendWithRetryAsync(
      () => new HttpRequestMessage(HttpMethod.Post, $"/api/repositories/{id}/stop"),
      "stop repository");
    await EnsureSuccessOrThrowAsync(response);
  }

  public async Task StartAllAsync()
  {
    HttpResponseMessage response = await SendWithRetryAsync(
      () => new HttpRequestMessage(HttpMethod.Post, "/api/repositories/start-all"),
      "start all repositories");
    await EnsureSuccessOrThrowAsync(response);
  }

  public async Task StopAllAsync()
  {
    HttpResponseMessage response = await SendWithRetryAsync(
      () => new HttpRequestMessage(HttpMethod.Post, "/api/repositories/stop-all"),
      "stop all repositories");
    await EnsureSuccessOrThrowAsync(response);
  }

  public async Task LoadConfigAsync(string configPath)
  {
    LoadConfigRequest request = new(configPath);
    string json = JsonSerializer.Serialize(request, AppJsonContext.Default.LoadConfigRequest);
    HttpResponseMessage response = await SendWithRetryAsync(
      () => new HttpRequestMessage(HttpMethod.Post, "/api/config/load")
      {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
      },
      "load config");
    await EnsureSuccessOrThrowAsync(response);
  }

  public async Task SaveConfigAsync(string configPath)
  {
    SaveConfigRequest request = new(configPath);
    string json = JsonSerializer.Serialize(request, AppJsonContext.Default.SaveConfigRequest);
    HttpResponseMessage response = await SendWithRetryAsync(
      () => new HttpRequestMessage(HttpMethod.Post, "/api/config/save")
      {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
      },
      "save config");
    await EnsureSuccessOrThrowAsync(response);
  }

  private async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> requestFactory, string operation)
  {
    HttpResponseMessage? lastResponse = null;
    Exception? lastException = null;

    for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
    {
      lastResponse?.Dispose();
      lastResponse = null;

      try
      {
        using HttpRequestMessage request = requestFactory();
        HttpResponseMessage response = await _httpClient.SendAsync(request);

        if (!ShouldRetry(response.StatusCode) || attempt == MaxRetryAttempts)
        {
          return response;
        }

        TimeSpan delay = GetRetryDelay(attempt);
        Log.Warning(
          "Daemon {Operation} attempt {Attempt}/{MaxAttempts} returned {StatusCode}, retrying in {DelayMs}ms",
          operation,
          attempt,
          MaxRetryAttempts,
          (int) response.StatusCode,
          (int) delay.TotalMilliseconds);
        response.Dispose();
        await Task.Delay(delay);
      }
      catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
      {
        lastException = ex;
        if (attempt == MaxRetryAttempts)
        {
          break;
        }

        TimeSpan delay = GetRetryDelay(attempt);
        Log.Warning(
          ex,
          "Daemon {Operation} attempt {Attempt}/{MaxAttempts} failed, retrying in {DelayMs}ms",
          operation,
          attempt,
          MaxRetryAttempts,
          (int) delay.TotalMilliseconds);
        await Task.Delay(delay);
      }
    }

    if (lastException != null)
    {
      throw new HttpRequestException($"Failed to {operation} after {MaxRetryAttempts} attempts", lastException);
    }

    return lastResponse ?? throw new HttpRequestException($"Failed to {operation}: no response received");
  }

  private static bool ShouldRetry(HttpStatusCode statusCode)
  {
    return statusCode == HttpStatusCode.RequestTimeout ||
           statusCode == HttpStatusCode.TooManyRequests ||
           (int) statusCode >= 500;
  }

  private static TimeSpan GetRetryDelay(int attempt)
  {
    return TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt - 1));
  }

  private static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response)
  {
    if (response.IsSuccessStatusCode)
    {
      return;
    }

    string errorBody = await response.Content.ReadAsStringAsync();
    throw new HttpRequestException($"Daemon returned {(int) response.StatusCode}: {errorBody}");
  }
}

public record LoadConfigRequest(string Path);

public record SaveConfigRequest(string Path);