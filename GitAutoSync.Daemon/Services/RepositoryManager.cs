using System.Collections.Concurrent;
using GitAutoSync.Core;
using GitAutoSync.Core.Config;
using GitAutoSync.Daemon.Models;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;
using Tomlet;

namespace GitAutoSync.Daemon.Services;

public class RepositoryManager : IDisposable
{
  private readonly ConcurrentDictionary<string, RepositoryWorkerState> _workers = new();
  private readonly ILoggerFactory _loggerFactory;
  private readonly EventBus _eventBus;

  public static readonly string DefaultConfigPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".git-auto-sync",
    "config.toml");

  public RepositoryManager(ILoggerFactory loggerFactory, EventBus eventBus)
  {
    _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    AutoLoadConfig();
  }

  public async Task<RepositoryInfo> AddRepositoryAsync(string path, string? name = null)
  {
    string repoName = name ?? new DirectoryInfo(path).Name;
    string id = Guid.NewGuid().ToString();

    if (!Directory.Exists(path))
    {
      throw new DirectoryNotFoundException($"Repository path does not exist: {path}");
    }

    if (!Directory.Exists(Path.Combine(path, ".git")))
    {
      throw new InvalidOperationException($"Path is not a git repository: {path}");
    }

    // Normalize path and check for duplicates
    path = Path.GetFullPath(path);

    if (_workers.Values.Any(s => s.Info.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
    {
      throw new InvalidOperationException($"Repository already exists at path: {path}");
    }

    RepositoryInfo info = new()
    {
      Id = id,
      Name = repoName,
      Path = path,
      IsRunning = false,
      Status = "Stopped",
    };

    GitAutoSyncDirectoryWorker worker = CreateWorker(repoName, path);
    RepositoryWorkerState state = new(info, worker);

    if (!_workers.TryAdd(id, state))
    {
      throw new InvalidOperationException($"Failed to add repository: {repoName}");
    }

    // Worker starts automatically, so mark as running
    state.Info.IsRunning = true;
    state.Info.Status = "Running";
    state.Info.LastActivity = DateTime.UtcNow;

    _eventBus.PublishStatusChange(id, true, "Running");
    _eventBus.PublishLog(repoName, "INFO", "Repository started");

    Serilog.Log.Information("Repository added and started: {Name} ({Id}) at {Path}", repoName, id, path);
    AutoSaveConfig();
    return info;
  }

  public async Task StartRepositoryAsync(string id)
  {
    if (!_workers.TryGetValue(id, out RepositoryWorkerState? state))
    {
      throw new KeyNotFoundException($"Repository not found: {id}");
    }

    if (state.Info.IsRunning)
    {
      Serilog.Log.Warning("Repository {Id} is already running", id);
      return;
    }

    // Worker is disposed when a repository is stopped; recreate it before starting again.
    state.Worker = CreateWorker(state.Info.Name, state.Info.Path);

    state.Info.IsRunning = true;
    state.Info.Status = "Running";
    state.Info.LastActivity = DateTime.UtcNow;

    _eventBus.PublishStatusChange(id, true, "Running");
    _eventBus.PublishLog(state.Info.Name, "INFO", "Repository started");

    Serilog.Log.Information("Repository started: {Name} ({Id})", state.Info.Name, id);
  }

  public async Task StopRepositoryAsync(string id)
  {
    if (!_workers.TryGetValue(id, out RepositoryWorkerState? state))
    {
      throw new KeyNotFoundException($"Repository not found: {id}");
    }

    if (!state.Info.IsRunning)
    {
      Serilog.Log.Warning("Repository {Id} is not running", id);
      return;
    }

    state.Worker.Dispose();
    state.Info.IsRunning = false;
    state.Info.Status = "Stopped";
    state.Info.LastActivity = DateTime.UtcNow;

    _eventBus.PublishStatusChange(id, false, "Stopped");
    _eventBus.PublishLog(state.Info.Name, "INFO", "Repository stopped");

    Serilog.Log.Information("Repository stopped: {Id}", id);
  }

  public async Task StartAllAsync()
  {
    foreach (RepositoryWorkerState state in _workers.Values.Where(s => !s.Info.IsRunning))
    {
      await StartRepositoryAsync(state.Info.Id);
    }
  }

  public async Task StopAllAsync()
  {
    List<string> runningIds = _workers.Values
      .Where(s => s.Info.IsRunning)
      .Select(s => s.Info.Id)
      .ToList();

    foreach (string id in runningIds)
    {
      await StopRepositoryAsync(id);
    }
  }

  public async Task RemoveRepositoryAsync(string id)
  {
    if (!_workers.TryRemove(id, out RepositoryWorkerState? state))
    {
      throw new KeyNotFoundException($"Repository not found: {id}");
    }

    if (state.Info.IsRunning)
    {
      state.Worker.Dispose();
      state.Info.IsRunning = false;
      state.Info.Status = "Stopped";
    }

    Serilog.Log.Information("Repository removed: {Id}", id);
    AutoSaveConfig();
  }

  public IEnumerable<RepositoryInfo> GetRepositories()
  {
    return _workers.Values.Select(s => s.Info).ToList();
  }

  public RepositoryInfo? GetRepository(string id)
  {
    return _workers.TryGetValue(id, out RepositoryWorkerState? state) ? state.Info : null;
  }

  public async Task LoadConfigAsync(string configPath)
  {
    if (!File.Exists(configPath))
    {
      throw new FileNotFoundException($"Config file not found: {configPath}");
    }

    string configContent = await File.ReadAllTextAsync(configPath);
    Config config = TomletMain.To<Config>(configContent);

    int loadedCount = 0;
    int skippedCount = 0;

    foreach (RepoConfig repoConfig in config.Repos)
    {
      if (string.IsNullOrWhiteSpace(repoConfig.Path) || string.IsNullOrWhiteSpace(repoConfig.Name))
      {
        Serilog.Log.Warning("Skipping repo with empty path or name");
        skippedCount++;
        continue;
      }

      // Filter by hostname
      if (!MatchesHostname(repoConfig.Hosts))
      {
        Serilog.Log.Information("Skipping repo {Name} - hostname filter does not match", repoConfig.Name);
        skippedCount++;
        continue;
      }

      // Check if already exists
      if (_workers.Values.Any(s => s.Info.Path.Equals(repoConfig.Path, StringComparison.OrdinalIgnoreCase)))
      {
        Serilog.Log.Information("Skipping repo {Name} - already loaded", repoConfig.Name);
        skippedCount++;
        continue;
      }

      try
      {
        await AddRepositoryAsync(repoConfig.Path, repoConfig.Name);
        loadedCount++;
      }
      catch (Exception ex)
      {
        Serilog.Log.Error(ex, "Failed to load repository {Name} from config", repoConfig.Name);
        skippedCount++;
      }
    }

    Serilog.Log.Information(
      "Config loaded: {LoadedCount} repositories loaded, {SkippedCount} skipped",
      loadedCount,
      skippedCount);
  }

  public async Task SaveConfigAsync(string configPath)
  {
    string? dir = Path.GetDirectoryName(configPath);
    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
    {
      Directory.CreateDirectory(dir);
    }

    Config config = new()
    {
      Repos = _workers.Values.Select(s => new RepoConfig
      {
        Name = s.Info.Name,
        Path = s.Info.Path,
        Hosts = new List<string>(),
      }).ToList(),
    };

    string toml = TomletMain.TomlStringFrom(config);
    await File.WriteAllTextAsync(configPath, toml);

    Serilog.Log.Information("Config saved to {ConfigPath} with {Count} repositories", configPath, config.Repos.Count);
  }

  private void AutoSaveConfig()
  {
    try
    {
      SaveConfigAsync(DefaultConfigPath).GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
      Serilog.Log.Warning(ex, "Failed to auto-save config");
    }
  }

  private void AutoLoadConfig()
  {
    try
    {
      if (!File.Exists(DefaultConfigPath))
      {
        return;
      }

      Serilog.Log.Information("Auto-loading config from {Path}", DefaultConfigPath);
      LoadConfigAsync(DefaultConfigPath).GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
      Serilog.Log.Warning(ex, "Failed to auto-load config");
    }
  }

  private bool MatchesHostname(List<string> hosts)
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

  public void Dispose()
  {
    AutoSaveConfig();
    foreach (RepositoryWorkerState state in _workers.Values)
    {
      state.Worker.Dispose();
    }

    _workers.Clear();
  }

  private GitAutoSyncDirectoryWorker CreateWorker(string repoName, string path)
  {
    ILogger<GitAutoSyncDirectoryWorker> logger = _loggerFactory.CreateLogger<GitAutoSyncDirectoryWorker>();
    return new GitAutoSyncDirectoryWorker(logger, repoName, path);
  }

  private class RepositoryWorkerState
  {
    public RepositoryInfo Info { get; }
    public GitAutoSyncDirectoryWorker Worker { get; set; }

    public RepositoryWorkerState(RepositoryInfo info, GitAutoSyncDirectoryWorker worker)
    {
      Info = info;
      Worker = worker;
    }
  }
}