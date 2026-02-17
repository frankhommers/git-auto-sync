using GitAutoSync.Daemon.Services;
using Microsoft.AspNetCore.Mvc;

namespace GitAutoSync.Daemon.Api;

public static class StartupEndpoints
{
  public static void MapStartupEndpoints(this WebApplication app)
  {
    RouteGroupBuilder group = app.MapGroup("/api/startup");

    // GET /api/startup/status - Check if startup on login is enabled
    group.MapGet(
      "/status",
      async (StartupService service) =>
      {
        bool isEnabled = await service.IsEnabledAsync();
        bool isSupported = service.IsSupported();
        string statusMessage = service.GetStatusMessage();

        return Results.Ok(
          new
          {
            enabled = isEnabled,
            supported = isSupported,
            message = statusMessage,
          });
      });

    // POST /api/startup/enable - Enable startup on login
    group.MapPost(
      "/enable",
      async ([FromBody] EnableStartupRequest request, StartupService service) =>
      {
        if (string.IsNullOrWhiteSpace(request.ConfigFilePath))
        {
          return Results.BadRequest("ConfigFilePath is required");
        }

        if (!File.Exists(request.ConfigFilePath))
        {
          return Results.BadRequest("Config file does not exist");
        }

        if (!service.IsSupported())
        {
          return Results.BadRequest("Startup on login is not supported on this platform");
        }

        bool result = await service.EnableAsync(request.ConfigFilePath);

        if (result)
        {
          return Results.Ok(new {success = true, message = "Startup on login enabled"});
        }
        else
        {
          return Results.StatusCode(500);
        }
      });

    // POST /api/startup/disable - Disable startup on login
    group.MapPost(
      "/disable",
      async (StartupService service) =>
      {
        if (!service.IsSupported())
        {
          return Results.BadRequest("Startup on login is not supported on this platform");
        }

        bool result = await service.DisableAsync();

        if (result)
        {
          return Results.Ok(new {success = true, message = "Startup on login disabled"});
        }
        else
        {
          return Results.StatusCode(500);
        }
      });
  }

  public record EnableStartupRequest(string ConfigFilePath);
}