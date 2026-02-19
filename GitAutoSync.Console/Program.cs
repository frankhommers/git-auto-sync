using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Cocona;
using Cocona.Builder;
using GitAutoSync.Core;
using GitAutoSync.Core.Config;
using Notifs;
using Tomlet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using WindowsShortcutFactory;

namespace GitAutoSync.Console;

internal class Program
{
  private static async Task<int> Main(string[] args)
  {
    Log.Logger = new LoggerConfiguration()
      .Enrich.FromLogContext()
      .WriteTo.Console(
        outputTemplate:
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3} {RepositoryName}] {Message:lj}{NewLine}{Exception}")
      .MinimumLevel.Debug()
      .CreateLogger();
    CoconaAppBuilder builder = CoconaApp.CreateBuilder(args, ConfigureCocona);
    builder.Host.UseSerilog();
    CoconaApp app = builder.Build();
    try
    {
      await app.RunAsync<Program>();
    }
    catch (Exception ex)
    {
      Log.Logger.Error(ex, "Error while running GitAutoSync");
      return 1;
    }
    finally
    {
      await Log.CloseAndFlushAsync();
    }

    return 0;
  }

  private static void ConfigureCocona(CoconaAppOptions options)
  {
    options.EnableShellCompletionSupport = true;
  }

  private static bool MatchesHostname(List<string> hosts)
  {
    if (!hosts.Any())
    {
      return true;
    }

    string machineName = Environment.MachineName;

    // First try exact match
    if (hosts.Contains(machineName))
    {
      return true;
    }

    // If no exact match and machine name contains a dot, try stripping the domain part
    if (machineName.Contains('.'))
    {
      string shortName = machineName.Split('.')[0];
      if (hosts.Contains(shortName))
      {
        return true;
      }
    }

    return false;
  }

  public async Task RunAsync(
    [FromService] ILogger<Program> logger,
    [FromService] IServiceProvider sp,
    [Option] string? configFile = null,
    [Option] bool addToLogin = false,
    [Option(Description = "Notification mode: auto-desktop-first, auto-terminal-first, desktop-only, terminal-only, off")]
    string notificationMode = "auto-terminal-first",
    [Option(Description = "Terminal notification preference: auto, osc9-only, bel-only")]
    string terminalNotificationPreference = "auto",
    [FromService] CancellationToken cancellationToken = default)
  {
    logger.LogInformation("Starting GitAutoSync Console");
    logger.LogInformation("Resolving config file location");
    string assemblyFile = Assembly.GetExecutingAssembly().Location;
    string? dir = Path.GetDirectoryName(assemblyFile);
    string tomlConfigFile;
    logger.LogInformation("Reading config");
    if (configFile == null)
    {
      configFile = Path.GetFileNameWithoutExtension(assemblyFile) + ".toml";
    }

    if (dir != null)
    {
      tomlConfigFile = Path.Combine(dir, configFile);
    }
    else
    {
      tomlConfigFile = Path.Combine(configFile);
    }

    logger.LogInformation("Config file location is: {ConfigFile}", tomlConfigFile);

    if (addToLogin)
    {
      logger.LogInformation("Adding to login");
      AddToLogin(logger, configFile);
      return;
    }

    Config config = TomletMain.To<Config>(await File.ReadAllTextAsync(tomlConfigFile));

    NotificationOptions notificationOptions = new()
    {
      Mode = ParseNotificationMode(notificationMode),
      TerminalPreference = ParseTerminalPreference(terminalNotificationPreference),
    };

    logger.LogInformation(
      "Notifications configured: mode={NotificationMode}, terminal={TerminalPreference}",
      notificationOptions.Mode,
      notificationOptions.TerminalPreference);

    List<GitAutoSyncDirectoryWorker> watchers = new();
    List<RepoConfig> repos = config.Repos
      .Where(repo => MatchesHostname(repo.Hosts)).ToList();

    logger.LogInformation("Starting watchers");
    foreach (RepoConfig repoConfig in repos)
    {
      if (string.IsNullOrWhiteSpace(repoConfig.Path) || string.IsNullOrWhiteSpace(repoConfig.Name))
      {
        logger.LogWarning("Repo path or name is empty, skipping");
        continue;
      }

      ILogger<GitAutoSyncDirectoryWorker>? subLogger = sp.GetService<ILogger<GitAutoSyncDirectoryWorker>>();
      if (subLogger == null)
      {
        logger.LogError("Failed to get logger for GitAutoSyncDirectoryWorker");
        continue;
      }

      using (IDisposable? scope =
             subLogger.BeginScope(new Dictionary<string, object> {{"RepositoryName", repoConfig.Name}}))
      {
        GitAutoSyncDirectoryWorker repoWatcher = new(subLogger, repoConfig.Name, repoConfig.Path, notificationOptions);
        watchers.Add(repoWatcher);
      }

      logger.LogInformation("Waiting 10 seconds before starting next watcher");
      await Task.Delay(10000);
    }

    while (!cancellationToken.IsCancellationRequested)
    {
      logger.LogInformation("GitAutoSync Console running, press Ctrl-C to quit");
      await Task.Delay(60 * 60 * 1000, cancellationToken);
    }
  }

  private static NotificationMode ParseNotificationMode(string input)
  {
    string normalized = input.Trim().ToLowerInvariant().Replace("_", "-");
    return normalized switch
    {
      "auto-desktop-first" => NotificationMode.AutoDesktopFirst,
      "auto-terminal-first" => NotificationMode.AutoTerminalFirst,
      "desktop-only" => NotificationMode.DesktopOnly,
      "terminal-only" => NotificationMode.TerminalOnly,
      "off" => NotificationMode.Off,
      _ => throw new Cocona.CommandExitedException(
        $"Invalid --notification-mode '{input}'. Expected: auto-desktop-first, auto-terminal-first, desktop-only, terminal-only, off.",
        2),
    };
  }

  private static TerminalNotificationPreference ParseTerminalPreference(string input)
  {
    string normalized = input.Trim().ToLowerInvariant().Replace("_", "-");
    return normalized switch
    {
      "auto" => TerminalNotificationPreference.Auto,
      "osc9-only" => TerminalNotificationPreference.Osc9Only,
      "bel-only" => TerminalNotificationPreference.BelOnly,
      _ => throw new Cocona.CommandExitedException(
        $"Invalid --terminal-notification-preference '{input}'. Expected: auto, osc9-only, bel-only.",
        2),
    };
  }

  private static void AddToLogin(ILogger<Program> logger, string configFile)
  {
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
      throw new NotImplementedException();
    }

    // /Users/frankhommers/Library/Logs/JetBrains/Toolbox/launchd-stderr.log
    AddToLoginWindows(logger, configFile);
    AddToLoginMacOS(logger, configFile);
  }

  private static void AddToLoginWindows(ILogger<Program> logger, string configFile)
  {
    logger.LogInformation("Adding to login");
    WindowsShortcut shortcut = new()
    {
      Path = @Assembly.GetExecutingAssembly().Location,
      ShowCommand = ProcessWindowStyle.Normal,
      Arguments = "--config-file \"" + configFile + "\"",
      Description = "GitAutoSync",
    };
    string shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "GitAutoSync.lnk");
    shortcut.Save(shortcutPath);
  }

  private static void AddToLoginMacOS(ILogger<Program> logger, string configFile)
  {
    string logFolder = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
      "Library",
      "Logs",
      "GitAutoSync");

    Directory.CreateDirectory(logFolder);
    string resourceName = "tools.franks.git-auto-sync";
    string plistFileName = $"{resourceName}.plist";
    string plistContent = GetEmbeddedResourceContents(plistFileName);
    string myExecutable = Regex.Replace(Assembly.GetExecutingAssembly().Location, @"\.dll$", "");
    plistContent = plistContent.Replace("{executablePath}", myExecutable);
    plistContent = plistContent.Replace("{stdErrLogFilename}", Path.Combine(logFolder, "GitAutoSync.Error.log"));
    plistContent = plistContent.Replace("{stdOutLogFilename}", Path.Combine(logFolder, "GitAutoSync.log"));
    plistContent = plistContent.Replace("{configFile}", configFile);
    string plistFullFilename = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
      "Library",
      "LaunchAgents",
      plistFileName);
    logger.LogInformation(
      "Plist: Location: {PlistFullFilename}, content: {PlistContent}",
      plistFullFilename,
      plistContent);
    File.WriteAllText(plistFullFilename, plistContent);
  }

  private static string GetEmbeddedResourceContents(string resourceName, Assembly? assembly = null)
  {
    assembly ??= Assembly.GetExecutingAssembly();
    string fullResourceName = assembly.GetManifestResourceNames().Single(r =>
                                                                           r.EndsWith(
                                                                             $".{resourceName}",
                                                                             StringComparison.OrdinalIgnoreCase) ||
                                                                           r.Equals(
                                                                             resourceName,
                                                                             StringComparison.OrdinalIgnoreCase));
    using Stream? stream = assembly.GetManifestResourceStream(fullResourceName);
    if (stream == null)
    {
      throw new InvalidOperationException($"Resource '{resourceName}' not found");
    }

    using StreamReader reader = new(stream);
    return reader.ReadToEnd();
  }
}
