using GitAutoSync.Daemon.Api;
using Serilog;

Log.Logger = new LoggerConfiguration()
  .WriteTo.Console()
  .WriteTo.File("logs/daemon-.log", rollingInterval: RollingInterval.Day)
  .CreateLogger();

try
{
  Log.Information("Git Auto Sync Daemon starting...");

  WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

  builder.Host.UseSerilog();

  // Register services
  builder.Services.AddSingleton<GitAutoSync.Daemon.Services.RepositoryManager>();
  builder.Services.AddSingleton<GitAutoSync.Daemon.Services.EventBus>();
  builder.Services.AddSingleton<WebSocketHandler>();
  builder.Services.AddSingleton<GitAutoSync.Core.IStartupManager, GitAutoSync.Core.StartupManager>();
  builder.Services.AddSingleton<GitAutoSync.Daemon.Services.StartupService>();

  WebApplication app = builder.Build();

  app.UseWebSockets();

  // Log raw request bodies for debugging
  app.Use(async (context, next) =>
  {
    if (context.Request.Method == "POST" && context.Request.ContentType?.Contains("json") == true)
    {
      context.Request.EnableBuffering();
      using StreamReader reader = new(context.Request.Body, leaveOpen: true);
      string body = await reader.ReadToEndAsync();
      context.Request.Body.Position = 0;
      Log.Information("Raw POST {Path} Body: {Body}", context.Request.Path, body);
    }

    await next();
  });

  app.Map(
    "/ws",
    async context =>
    {
      WebSocketHandler handler = context.RequestServices.GetRequiredService<WebSocketHandler>();
      await handler.HandleWebSocketAsync(context);
    });

  // Health check endpoint
  string daemonVersion = typeof(GitAutoSync.Daemon.Program).Assembly.GetName().Version?.ToString() ?? "unknown";
  app.MapGet("/health", () => Results.Ok(new {status = "healthy", version = daemonVersion}));

  // Map API endpoints
  app.MapRepositoriesEndpoints();
  app.MapStartupEndpoints();
  app.MapConfigEndpoints();

  app.Run();
}
catch (Exception ex)
{
  Log.Fatal(ex, "Daemon terminated unexpectedly");
}
finally
{
  Log.CloseAndFlush();
}

// Required for WebApplicationFactory in integration tests
namespace GitAutoSync.Daemon
{
  public partial class Program
  {
  }
}
