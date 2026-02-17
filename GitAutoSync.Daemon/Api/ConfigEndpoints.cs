using GitAutoSync.Daemon.Services;
using Microsoft.AspNetCore.Mvc;

namespace GitAutoSync.Daemon.Api;

public static class ConfigEndpoints
{
  public static void MapConfigEndpoints(this WebApplication app)
  {
    RouteGroupBuilder group = app.MapGroup("/api/config");

    group.MapPost(
      "/load",
      async ([FromBody] LoadConfigRequest request, RepositoryManager manager) =>
      {
        if (string.IsNullOrWhiteSpace(request.Path))
        {
          return Results.BadRequest("Path is required");
        }

        if (!File.Exists(request.Path))
        {
          return Results.BadRequest($"Config file not found: {request.Path}");
        }

        try
        {
          await manager.LoadConfigAsync(request.Path);
          return Results.Ok(new {message = "Config loaded successfully"});
        }
        catch (Exception ex)
        {
          Serilog.Log.Error(ex, "Failed to load config from {Path}", request.Path);
          return Results.Problem($"Failed to load config: {ex.Message}");
        }
      });

    group.MapPost(
      "/save",
      async ([FromBody] SaveConfigRequest request, RepositoryManager manager) =>
      {
        if (string.IsNullOrWhiteSpace(request.Path))
        {
          return Results.BadRequest("Path is required");
        }

        try
        {
          await manager.SaveConfigAsync(request.Path);
          return Results.Ok(new {message = "Config saved successfully", path = request.Path});
        }
        catch (Exception ex)
        {
          Serilog.Log.Error(ex, "Failed to save config to {Path}", request.Path);
          return Results.Problem($"Failed to save config: {ex.Message}");
        }
      });
  }

  public record LoadConfigRequest(string Path);

  public record SaveConfigRequest(string Path);
}