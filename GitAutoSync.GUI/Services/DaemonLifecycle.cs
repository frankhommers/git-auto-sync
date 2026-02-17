using System.Diagnostics;
using System.Reflection;
using Serilog;

namespace GitAutoSync.GUI.Services;

public class DaemonLifecycle : IDisposable
{
  private readonly DaemonClient _client;
  private Process? _daemonProcess;

  public DaemonLifecycle(DaemonClient client)
  {
    _client = client;
  }

  public async Task<bool> EnsureDaemonRunningAsync()
  {
    if (await _client.IsHealthyAsync())
    {
      Log.Information("Daemon already running");
      return true;
    }

    return await StartDaemonAsync();
  }

  private async Task<bool> StartDaemonAsync()
  {
    try
    {
      // Try published executable first, then fall back to dotnet run
      (string fileName, string arguments, string workingDirectory) = GetDaemonStartInfo();
      Log.Information("Starting daemon: {FileName} {Arguments}", fileName, arguments);

      _daemonProcess = new Process
      {
        StartInfo = new ProcessStartInfo
        {
          FileName = fileName,
          Arguments = arguments,
          WorkingDirectory = workingDirectory,
          UseShellExecute = false,
          CreateNoWindow = true,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
        },
      };

      _daemonProcess.OutputDataReceived += (_, e) =>
      {
        if (!string.IsNullOrEmpty(e.Data))
        {
          Log.Debug("[Daemon] {Data}", e.Data);
        }
      };
      _daemonProcess.ErrorDataReceived += (_, e) =>
      {
        if (!string.IsNullOrEmpty(e.Data))
        {
          Log.Warning("[Daemon ERR] {Data}", e.Data);
        }
      };

      _daemonProcess.Start();
      _daemonProcess.BeginOutputReadLine();
      _daemonProcess.BeginErrorReadLine();

      // Wait for daemon to be ready (up to 15 seconds)
      for (int i = 0; i < 30; i++)
      {
        await Task.Delay(500);

        if (_daemonProcess.HasExited)
        {
          Log.Error("Daemon process exited prematurely with code {ExitCode}", _daemonProcess.ExitCode);
          return false;
        }

        if (await _client.IsHealthyAsync())
        {
          Log.Information("Daemon is healthy and ready");
          return true;
        }
      }

      Log.Error("Daemon health check timed out after 15 seconds");
      return false;
    }
    catch (Exception ex)
    {
      Log.Error(ex, "Failed to start daemon");
      return false;
    }
  }

  private (string fileName, string arguments, string workingDirectory) GetDaemonStartInfo()
  {
    const string daemonUrl = "http://127.0.0.1:52847";

    // 1. Check for published executable next to GUI
    string baseDir = AppContext.BaseDirectory;
    string exeName = OperatingSystem.IsWindows() ? "GitAutoSync.Daemon.exe" : "GitAutoSync.Daemon";
    string exePath = Path.Combine(baseDir, exeName);

    if (File.Exists(exePath))
    {
      return (exePath, $"--urls {daemonUrl}", baseDir);
    }

    // 2. Fall back to dotnet run with project path (development mode)
    string? projectPath = FindDaemonProject();
    if (projectPath != null)
    {
      string projectDir = Path.GetDirectoryName(projectPath)!;
      return ("dotnet", $"run --project \"{projectPath}\" -- --urls {daemonUrl}", projectDir);
    }

    throw new FileNotFoundException(
      $"Daemon not found. Looked for executable at '{exePath}' and project file in parent directories.");
  }

  private string? FindDaemonProject()
  {
    // Walk up from the GUI base directory to find the repo root with GitAutoSync.Daemon
    DirectoryInfo? dir = new(AppContext.BaseDirectory);

    while (dir != null)
    {
      string candidate = Path.Combine(dir.FullName, "GitAutoSync.Daemon", "GitAutoSync.Daemon.csproj");
      if (File.Exists(candidate))
      {
        return candidate;
      }

      dir = dir.Parent;
    }

    return null;
  }

  public void StopDaemon()
  {
    if (_daemonProcess != null && !_daemonProcess.HasExited)
    {
      _daemonProcess.Kill(true);
      _daemonProcess.Dispose();
      _daemonProcess = null;
    }
  }

  public void Dispose()
  {
    StopDaemon();
  }
}