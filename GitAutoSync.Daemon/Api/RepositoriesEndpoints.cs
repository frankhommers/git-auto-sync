using System.Text.Json;
using GitAutoSync.Daemon.Api.Models;
using GitAutoSync.Daemon.Models;
using GitAutoSync.Daemon.Services;
using Serilog;

namespace GitAutoSync.Daemon.Api;

public static class RepositoriesEndpoints
{
  // Explicit deserialization options - no reliance on ASP.NET model binding
  private static readonly JsonSerializerOptions _jsonOptions = new()
  {
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
  };

  public static void MapRepositoriesEndpoints(this WebApplication app)
  {
    RouteGroupBuilder group = app.MapGroup("/api/repositories");

    group.MapGet(
      "/",
      (RepositoryManager manager) =>
      {
        IEnumerable<RepositoryInfo> repos = manager.GetRepositories();
        return Results.Ok(repos);
      });

    group.MapGet(
      "/{id}",
      (string id, RepositoryManager manager) =>
      {
        RepositoryInfo? repo = manager.GetRepository(id);
        return repo != null ? Results.Ok(repo) : Results.NotFound();
      });

    group.MapPost(
      "/",
      async (HttpContext context, RepositoryManager manager) =>
      {
        // Manually read and deserialize - bypass ASP.NET model binding
        string body;
        using (StreamReader reader = new(context.Request.Body))
        {
          body = await reader.ReadToEndAsync();
        }

        Log.Information("POST /api/repositories raw body: {Body}", body);

        AddRepositoryRequest? request;
        try
        {
          request = JsonSerializer.Deserialize<AddRepositoryRequest>(body, _jsonOptions);
        }
        catch (JsonException ex)
        {
          Log.Error(ex, "Failed to deserialize request body");
          return Results.BadRequest($"Invalid JSON: {ex.Message}");
        }

        if (request == null)
        {
          Log.Warning("Deserialized request is null");
          return Results.BadRequest("Invalid request body");
        }

        Log.Information("Deserialized: Name={Name}, Path={Path}", request.Name, request.Path);

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Path))
        {
          return Results.BadRequest($"Name and Path are required. Got Name='{request.Name}', Path='{request.Path}'");
        }

        if (!Directory.Exists(request.Path))
        {
          return Results.BadRequest($"Path does not exist: {request.Path}");
        }

        string gitPath = Path.Combine(request.Path, ".git");
        if (!Directory.Exists(gitPath))
        {
          return Results.BadRequest($"Path is not a git repository: {request.Path}");
        }

        try
        {
          RepositoryInfo repo = await manager.AddRepositoryAsync(request.Path, request.Name);
          return Results.Created($"/api/repositories/{repo.Id}", repo);
        }
        catch (InvalidOperationException ex)
        {
          return Results.BadRequest(ex.Message);
        }
      });

    group.MapDelete(
      "/{id}",
      async (string id, RepositoryManager manager) =>
      {
        try
        {
          await manager.RemoveRepositoryAsync(id);
          return Results.NoContent();
        }
        catch (KeyNotFoundException)
        {
          return Results.NotFound();
        }
      });

    group.MapPost(
      "/{id}/start",
      async (string id, RepositoryManager manager) =>
      {
        try
        {
          await manager.StartRepositoryAsync(id);
          return Results.NoContent();
        }
        catch (KeyNotFoundException)
        {
          return Results.NotFound();
        }
      });

    group.MapPost(
      "/{id}/stop",
      async (string id, RepositoryManager manager) =>
      {
        try
        {
          await manager.StopRepositoryAsync(id);
          return Results.NoContent();
        }
        catch (KeyNotFoundException)
        {
          return Results.NotFound();
        }
      });

    group.MapPost(
      "/start-all",
      async (RepositoryManager manager) =>
      {
        await manager.StartAllAsync();
        return Results.NoContent();
      });

    group.MapPost(
      "/stop-all",
      async (RepositoryManager manager) =>
      {
        await manager.StopAllAsync();
        return Results.NoContent();
      });
  }
}