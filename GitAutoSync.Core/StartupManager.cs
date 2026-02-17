using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;

namespace GitAutoSync.Core;

public interface IStartupManager
{
  Task<bool> IsEnabledAsync();
  Task<bool> EnableAsync(string configFilePath);
  Task<bool> DisableAsync();
  bool IsSupported { get; }
  string GetStatusMessage();
}

public class StartupManager : IStartupManager
{
  private readonly ILogger<StartupManager> _logger;

  public StartupManager(ILogger<StartupManager>? logger = null)
  {
    _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<StartupManager>.Instance;
  }

  public bool IsSupported => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
                             RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
                             RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

  public async Task<bool> IsEnabledAsync()
  {
    try
    {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      {
        return await IsEnabledWindowsAsync();
      }

      if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      {
        return await IsEnabledMacOSAsync();
      }

      if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
      {
        return await IsEnabledLinuxAsync();
      }

      return false;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to check if startup is enabled");
      return false;
    }
  }

  public async Task<bool> EnableAsync(string configFilePath)
  {
    try
    {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      {
        return await EnableWindowsAsync(configFilePath);
      }

      if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      {
        return await EnableMacOSAsync(configFilePath);
      }

      if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
      {
        return await EnableLinuxAsync(configFilePath);
      }

      return false;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to enable startup");
      return false;
    }
  }

  public async Task<bool> DisableAsync()
  {
    try
    {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      {
        return await DisableWindowsAsync();
      }

      if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      {
        return await DisableMacOSAsync();
      }

      if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
      {
        return await DisableLinuxAsync();
      }

      return false;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to disable startup");
      return false;
    }
  }

  public string GetStatusMessage()
  {
    if (!IsSupported)
    {
      return "Startup on login is not supported on this platform";
    }

    string platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" :
      RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS" :
      RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" : "Unknown";

    return $"Startup on login support available for {platform}";
  }

  // Windows implementation
  private async Task<bool> IsEnabledWindowsAsync()
  {
    string shortcutPath = GetWindowsShortcutPath();
    return await Task.FromResult(File.Exists(shortcutPath));
  }

  private async Task<bool> EnableWindowsAsync(string configFilePath)
  {
    try
    {
      string executablePath = GetExecutablePath();
      string shortcutPath = GetWindowsShortcutPath();

      // For Windows, we'll create a batch file that starts the GUI
      string batchContent =
        $"@echo off\nstart \"GitAutoSync\" \"{executablePath}\" --config-file \"{configFilePath}\" --minimized --auto-start";
      string batchPath = Path.ChangeExtension(shortcutPath, ".bat");

      await File.WriteAllTextAsync(batchPath, batchContent);
      _logger.LogInformation("Created Windows startup batch file at: {Path}", batchPath);

      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to enable Windows startup");
      return false;
    }
  }

  private async Task<bool> DisableWindowsAsync()
  {
    try
    {
      string shortcutPath = GetWindowsShortcutPath();
      string batchPath = Path.ChangeExtension(shortcutPath, ".bat");

      if (File.Exists(batchPath))
      {
        File.Delete(batchPath);
        _logger.LogInformation("Removed Windows startup batch file");
      }

      return await Task.FromResult(true);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to disable Windows startup");
      return false;
    }
  }

  private string GetWindowsShortcutPath()
  {
    string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
    return Path.Combine(startupFolder, "GitAutoSync.lnk");
  }

  // macOS implementation
  private async Task<bool> IsEnabledMacOSAsync()
  {
    string plistPath = GetMacOSPlistPath();
    return await Task.FromResult(File.Exists(plistPath));
  }

  private async Task<bool> EnableMacOSAsync(string configFilePath)
  {
    try
    {
      string executablePath = GetExecutablePath();
      string plistPath = GetMacOSPlistPath();
      string logFolder = GetMacOSLogFolder();

      Directory.CreateDirectory(logFolder);
      Directory.CreateDirectory(Path.GetDirectoryName(plistPath)!);

      string plistContent = CreateMacOSPlistContent(
        executablePath,
        configFilePath,
        Path.Combine(logFolder, "GitAutoSync-GUI.log"),
        Path.Combine(logFolder, "GitAutoSync-GUI.Error.log"));

      await File.WriteAllTextAsync(plistPath, plistContent);
      _logger.LogInformation("Created macOS launch agent at: {Path}", plistPath);

      // Load the launch agent
      (int exitCode, string stdout, string stderr) result = await RunProcessAsync("launchctl", $"load \"{plistPath}\"");
      if (result.exitCode == 0)
      {
        _logger.LogInformation("Launch agent loaded successfully");
        return true;
      }
      else
      {
        _logger.LogWarning("Launch agent load warning: {Error}", result.stderr);
        return true; // Still consider it successful as the file was created
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to enable macOS startup");
      return false;
    }
  }

  private async Task<bool> DisableMacOSAsync()
  {
    try
    {
      string plistPath = GetMacOSPlistPath();

      if (File.Exists(plistPath))
      {
        // Unload the launch agent
        await RunProcessAsync("launchctl", $"unload \"{plistPath}\"");

        // Delete the plist file
        File.Delete(plistPath);
        _logger.LogInformation("Removed macOS launch agent");
      }

      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to disable macOS startup");
      return false;
    }
  }

  private string GetMacOSPlistPath()
  {
    return Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
      "Library",
      "LaunchAgents",
      "tools.franks.git-auto-sync-gui.plist");
  }

  private string GetMacOSLogFolder()
  {
    return Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
      "Library",
      "Logs",
      "GitAutoSync");
  }

  private string CreateMacOSPlistContent(string executablePath, string configFile, string stdOutPath, string stdErrPath)
  {
    return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
                <dict>
                    <key>Label</key>
                    <string>tools.franks.git-auto-sync-gui</string>
                    <key>ProgramArguments</key>
                    <array>
                        <string>{executablePath}</string>
                        <string>--config-file</string>
                        <string>{configFile}</string>
                        <string>--minimized</string>
                        <string>--auto-start</string>
                    </array>
                    <key>RunAtLoad</key>
                    <true/>
                    <key>StandardOutPath</key>
                    <string>{stdOutPath}</string>
                    <key>StandardErrorPath</key>
                    <string>{stdErrPath}</string>
                </dict>
            </plist>
            """;
  }

  // Linux implementation
  private async Task<bool> IsEnabledLinuxAsync()
  {
    string desktopFilePath = GetLinuxDesktopFilePath();
    return await Task.FromResult(File.Exists(desktopFilePath));
  }

  private async Task<bool> EnableLinuxAsync(string configFilePath)
  {
    try
    {
      string executablePath = GetExecutablePath();
      string desktopFilePath = GetLinuxDesktopFilePath();

      Directory.CreateDirectory(Path.GetDirectoryName(desktopFilePath)!);

      string desktopContent = CreateLinuxDesktopFileContent(executablePath, configFilePath);
      await File.WriteAllTextAsync(desktopFilePath, desktopContent);

      // Make the file executable
      await RunProcessAsync("chmod", $"+x \"{desktopFilePath}\"");

      _logger.LogInformation("Created Linux autostart desktop file at: {Path}", desktopFilePath);
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to enable Linux startup");
      return false;
    }
  }

  private async Task<bool> DisableLinuxAsync()
  {
    try
    {
      string desktopFilePath = GetLinuxDesktopFilePath();

      if (File.Exists(desktopFilePath))
      {
        File.Delete(desktopFilePath);
        _logger.LogInformation("Removed Linux autostart desktop file");
      }

      return await Task.FromResult(true);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to disable Linux startup");
      return false;
    }
  }

  private string GetLinuxDesktopFilePath()
  {
    string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ??
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
    return Path.Combine(configHome, "autostart", "git-auto-sync.desktop");
  }

  private string CreateLinuxDesktopFileContent(string executablePath, string configFile)
  {
    return $"""
            [Desktop Entry]
            Type=Application
            Name=Git Auto Sync
            Comment=Automatically sync your git repositories
            Exec={executablePath} --config-file "{configFile}" --minimized --auto-start
            Icon=git
            Hidden=false
            NoDisplay=false
            X-GNOME-Autostart-enabled=true
            """;
  }

  // Helper methods
  private string GetExecutablePath()
  {
    // Get the entry assembly (the main application) instead of the executing assembly (this library)
    Assembly? entryAssembly = Assembly.GetEntryAssembly();
    if (entryAssembly != null)
    {
      string entryLocation = entryAssembly.Location;
      return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? entryLocation : entryLocation.Replace(".dll", "");
    }

    // Fallback to executing assembly location
    string fallbackLocation = Assembly.GetExecutingAssembly().Location;
    return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
      ? fallbackLocation
      : fallbackLocation.Replace(".dll", "");
  }

  private async Task<(int exitCode, string stdout, string stderr)> RunProcessAsync(string fileName, string arguments)
  {
    using Process process = new();
    process.StartInfo.FileName = fileName;
    process.StartInfo.Arguments = arguments;
    process.StartInfo.UseShellExecute = false;
    process.StartInfo.RedirectStandardOutput = true;
    process.StartInfo.RedirectStandardError = true;
    process.StartInfo.CreateNoWindow = true;

    StringBuilder stdout = new();
    StringBuilder stderr = new();

    process.OutputDataReceived += (_, e) =>
    {
      if (e.Data != null)
      {
        stdout.AppendLine(e.Data);
      }
    };
    process.ErrorDataReceived += (_, e) =>
    {
      if (e.Data != null)
      {
        stderr.AppendLine(e.Data);
      }
    };

    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    await process.WaitForExitAsync();

    return (process.ExitCode, stdout.ToString(), stderr.ToString());
  }
}
