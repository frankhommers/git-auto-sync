using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using GitAutoSync.Core;
using Serilog;
using ILogger = Serilog.ILogger;
using GitAutoSync.GUI.Commands;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using GitAutoSync.GUI.Services;
using GitAutoSync.GUI.Models;
using System.Text.Json;
using Avalonia.Input.Platform;

namespace GitAutoSync.GUI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
  private string _configFilePath = "";
  private string _statusMessage = "Ready";
  private bool _isRunning = false;
  private RepositoryViewModel? _selectedRepository;
  private bool _isStartupEnabled = false;

  private readonly IStartupManager _startupManager;
  private readonly DaemonClient _daemonClient;
  private readonly DaemonWebSocketClient _webSocketClient;

  private bool _uiReady = false;
  private bool _shouldAutoStart = false;

  public ObservableCollection<RepositoryViewModel> Repositories { get; } = new();
  public ObservableCollection<LogEntryViewModel> LogEntries { get; } = new();

  private string _logText = "";

  public string LogText
  {
    get => _logText;
    set
    {
      if (Dispatcher.UIThread.CheckAccess())
      {
        this.RaiseAndSetIfChanged(ref _logText, value);
      }
      else
      {
        Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _logText, value));
      }
    }
  }

  public string ConfigFilePath
  {
    get => _configFilePath;
    set
    {
      if (Dispatcher.UIThread.CheckAccess())
      {
        this.RaiseAndSetIfChanged(ref _configFilePath, value);
      }
      else
      {
        Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _configFilePath, value));
      }
    }
  }

  public string StatusMessage
  {
    get => _statusMessage;
    set
    {
      if (Dispatcher.UIThread.CheckAccess())
      {
        this.RaiseAndSetIfChanged(ref _statusMessage, value);
      }
      else
      {
        Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _statusMessage, value));
      }
    }
  }

  public bool IsRunning
  {
    get => _isRunning;
    set
    {
      if (Dispatcher.UIThread.CheckAccess())
      {
        this.RaiseAndSetIfChanged(ref _isRunning, value);
      }
      else
      {
        Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _isRunning, value));
      }
    }
  }

  public RepositoryViewModel? SelectedRepository
  {
    get => _selectedRepository;
    set => this.RaiseAndSetIfChanged(ref _selectedRepository, value);
  }

  private bool _canStartAll = false;
  private bool _canStopAll = false;
  private int _activeRepositories = 0;
  private int _totalRepositories = 0;

  public bool CanStartAll
  {
    get => _canStartAll;
    private set
    {
      if (Dispatcher.UIThread.CheckAccess())
      {
        this.RaiseAndSetIfChanged(ref _canStartAll, value);
      }
      else
      {
        Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _canStartAll, value));
      }
    }
  }

  public bool IsStartupEnabled
  {
    get => _isStartupEnabled;
    private set
    {
      if (Dispatcher.UIThread.CheckAccess())
      {
        this.RaiseAndSetIfChanged(ref _isStartupEnabled, value);
      }
      else
      {
        Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _isStartupEnabled, value));
      }
    }
  }

  public bool CanStopAll
  {
    get => _canStopAll;
    private set
    {
      if (Dispatcher.UIThread.CheckAccess())
      {
        this.RaiseAndSetIfChanged(ref _canStopAll, value);
      }
      else
      {
        Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _canStopAll, value));
      }
    }
  }

  public int ActiveRepositories
  {
    get => _activeRepositories;
    private set
    {
      if (Dispatcher.UIThread.CheckAccess())
      {
        this.RaiseAndSetIfChanged(ref _activeRepositories, value);
      }
      else
      {
        Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _activeRepositories, value));
      }
    }
  }

  public int TotalRepositories
  {
    get => _totalRepositories;
    private set
    {
      if (Dispatcher.UIThread.CheckAccess())
      {
        this.RaiseAndSetIfChanged(ref _totalRepositories, value);
      }
      else
      {
        Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _totalRepositories, value));
      }
    }
  }

  // Commands
  public ICommand BrowseConfigCommand { get; }
  public ICommand SaveConfigCommand { get; }
  public ICommand AddRepoCommand { get; }
  public ICommand StartAllCommand { get; }
  public ICommand StopAllCommand { get; }
  public ICommand StartRepoCommand { get; }
  public ICommand StopRepoCommand { get; }
  public ICommand RemoveRepoCommand { get; }
  public ICommand ClearLogCommand { get; }
  public ICommand ToggleStartupCommand { get; }
  public ICommand ShowAboutCommand { get; }
  public ICommand CopyLogCommand { get; }

  public MainWindowViewModel(
    DaemonClient daemonClient,
    DaemonWebSocketClient webSocketClient,
    string? configFilePath = null,
    bool autoStart = false)
  {
    // Store auto-start flag for later use when UI is ready
    _shouldAutoStart = autoStart;

    // Initialize daemon services
    _daemonClient = daemonClient;
    _webSocketClient = webSocketClient;

    // Initialize startup manager
    _startupManager = new StartupManager();

    // Initialize config path - use provided path or default
    if (!string.IsNullOrEmpty(configFilePath))
    {
      ConfigFilePath = configFilePath;
    }
    else
    {
      string appDirectory = AppContext.BaseDirectory;
      ConfigFilePath = Path.Combine(appDirectory, "GitAutoSync.toml");
    }

    // Initialize commands - use thread-safe commands to avoid threading issues
    BrowseConfigCommand = new ThreadSafeCommand(BrowseConfig);
    SaveConfigCommand = new ThreadSafeCommand(SaveConfig);
    AddRepoCommand = new ThreadSafeCommand(AddRepository);
    StartAllCommand = new ThreadSafeCommand(StartAll, () => CanStartAll);
    StopAllCommand = new ThreadSafeCommand(StopAll, () => CanStopAll);
    StartRepoCommand = new ThreadSafeCommand<RepositoryViewModel>(async repo =>
    {
      if (repo != null)
      {
        await StartRepository(repo);
      }
    });
    StopRepoCommand = new ThreadSafeCommand<RepositoryViewModel>(async repo =>
    {
      if (repo != null)
      {
        await StopRepository(repo);
      }
    });
    RemoveRepoCommand = new ThreadSafeCommand<RepositoryViewModel>(repo =>
    {
      if (repo != null)
      {
        RemoveRepository(repo);
      }
    });
    ClearLogCommand = new ThreadSafeCommand(ClearLog);
    ToggleStartupCommand = new ThreadSafeCommand(ToggleStartup);
    ShowAboutCommand = new ThreadSafeCommand(ShowAbout);
    CopyLogCommand = new ThreadSafeCommand(CopyLog);

    // Subscribe to collection changes to update computed properties
    Repositories.CollectionChanged += (_, _) =>
    {
      if (Dispatcher.UIThread.CheckAccess())
      {
        UpdateComputedProperties();
      }
      else
      {
        Dispatcher.UIThread.Post(UpdateComputedProperties);
      }
    };

    // Setup WebSocket event handling
    _webSocketClient.EventReceived += OnDaemonEvent;

    // Setup logging
    SetupLogging();

    // Initialize computed properties
    UpdateComputedProperties();

    AddLogEntry("INFO", "Application", "Git Auto Sync started");
    AddLogEntry("INFO", "Daemon", "Waiting for daemon to start...");
  }

  public void OnDaemonReady(bool daemonReady)
  {
    _ = Task.Run(async () =>
    {
      if (!daemonReady)
      {
        AddLogEntry("ERROR", "Daemon", "Failed to start daemon. Retrying...");

        // Retry connecting to daemon with backoff
        for (int attempt = 1; attempt <= 5; attempt++)
        {
          await Task.Delay(attempt * 1000);
          if (await _daemonClient.IsHealthyAsync())
          {
            AddLogEntry("INFO", "Daemon", $"Daemon became available on retry {attempt}");
            daemonReady = true;
            break;
          }
        }

        if (!daemonReady)
        {
          AddLogEntry("ERROR", "Daemon", "Daemon is not available. Please restart the application.");
          StatusMessage = "Daemon unavailable";
          return;
        }
      }

      AddLogEntry("INFO", "Daemon", "Daemon is running, connecting...");

      try
      {
        await _webSocketClient.ConnectAsync();
        AddLogEntry("INFO", "Daemon", "Connected to daemon WebSocket");
      }
      catch (Exception ex)
      {
        AddLogEntry("WARNING", "Daemon", $"WebSocket connection failed: {ex.Message}");
      }

      try
      {
        await LoadRepositoriesFromDaemon();
      }
      catch (Exception ex)
      {
        AddLogEntry("ERROR", "Daemon", $"Failed to load repositories: {ex.Message}");
      }

      try
      {
        await CheckStartupStatus();
      }
      catch (Exception ex)
      {
        AddLogEntry("WARNING", "Daemon", $"Failed to check startup status: {ex.Message}");
      }

      StatusMessage = "Connected to daemon";
    });
  }

  private void SetupLogging()
  {
    Log.Logger = new LoggerConfiguration()
      .Enrich.FromLogContext()
      .WriteTo.Console()
      .WriteTo.Sink(new GuiLogSink(this))
      .MinimumLevel.Debug()
      .CreateLogger();
  }

  private async Task LoadRepositoriesFromDaemon()
  {
    try
    {
      List<RepositoryDto> repos = await _daemonClient.GetRepositoriesAsync();

      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        Repositories.Clear();

        foreach (RepositoryDto repoDto in repos)
        {
          RepositoryViewModel repoVm = new(repoDto.Name, repoDto.Path)
          {
            Id = repoDto.Id,
            IsRunning = repoDto.IsRunning,
            Status = repoDto.Status,
            LastActivity = repoDto.LastActivity?.ToString("HH:mm:ss") ?? "",
          };
          Repositories.Add(repoVm);
        }

        StatusMessage = $"Loaded {Repositories.Count} repositories from daemon";
        UpdateComputedProperties();

        // Force property change notifications
        this.RaisePropertyChanged(nameof(Repositories));
        this.RaisePropertyChanged(nameof(TotalRepositories));
        this.RaisePropertyChanged(nameof(CanStartAll));
        this.RaisePropertyChanged(nameof(StatusMessage));
      });

      AddLogEntry("INFO", "Daemon", $"Loaded {Repositories.Count} repositories from daemon");
    }
    catch (Exception ex)
    {
      AddLogEntry("ERROR", "Daemon", $"Failed to load repositories from daemon: {ex.Message}");
    }
  }

  private void OnDaemonEvent(object? sender, DaemonEventArgs e)
  {
    DaemonEvent evt = e.Event;

    Dispatcher.UIThread.InvokeAsync(() =>
    {
      try
      {
        switch (evt.Type)
        {
          case "repository_started":
            HandleRepositoryStarted(evt.Data);
            break;
          case "repository_stopped":
            HandleRepositoryStopped(evt.Data);
            break;
          case "repository_activity":
            HandleRepositoryActivity(evt.Data);
            break;
          case "repository_added":
            HandleRepositoryAdded(evt.Data);
            break;
          case "repository_removed":
            HandleRepositoryRemoved(evt.Data);
            break;
        }
      }
      catch (Exception ex)
      {
        AddLogEntry("ERROR", "WebSocket", $"Error handling event: {ex.Message}");
      }
    });
  }

  private void HandleRepositoryStarted(JsonElement data)
  {
    string? id = data.GetProperty("id").GetString();
    RepositoryViewModel? repo = Repositories.FirstOrDefault(r => r.Id == id);
    if (repo != null)
    {
      repo.IsRunning = true;
      repo.Status = "Running";
      repo.LastActivity = DateTime.Now.ToString("HH:mm:ss");
      UpdateComputedProperties();
      AddLogEntry("INFO", repo.Name, "Repository started");
    }
  }

  private void HandleRepositoryStopped(JsonElement data)
  {
    string? id = data.GetProperty("id").GetString();
    RepositoryViewModel? repo = Repositories.FirstOrDefault(r => r.Id == id);
    if (repo != null)
    {
      repo.IsRunning = false;
      repo.Status = "Stopped";
      repo.LastActivity = DateTime.Now.ToString("HH:mm:ss");
      UpdateComputedProperties();
      AddLogEntry("INFO", repo.Name, "Repository stopped");
    }
  }

  private void HandleRepositoryActivity(JsonElement data)
  {
    string? id = data.GetProperty("id").GetString();
    string message = data.GetProperty("message").GetString() ?? "";
    RepositoryViewModel? repo = Repositories.FirstOrDefault(r => r.Id == id);
    if (repo != null)
    {
      repo.LastActivity = DateTime.Now.ToString("HH:mm:ss");
      AddLogEntry("INFO", repo.Name, message);
    }
  }

  private void HandleRepositoryAdded(JsonElement data)
  {
    string id = data.GetProperty("id").GetString() ?? "";
    string name = data.GetProperty("name").GetString() ?? "";
    string path = data.GetProperty("path").GetString() ?? "";

    RepositoryViewModel repoVm = new(name, path)
    {
      Id = id,
      Status = "Stopped",
    };
    Repositories.Add(repoVm);
    UpdateComputedProperties();
    AddLogEntry("INFO", name, "Repository added");
  }

  private void HandleRepositoryRemoved(JsonElement data)
  {
    string? id = data.GetProperty("id").GetString();
    RepositoryViewModel? repo = Repositories.FirstOrDefault(r => r.Id == id);
    if (repo != null)
    {
      Repositories.Remove(repo);
      UpdateComputedProperties();
      AddLogEntry("INFO", repo.Name, "Repository removed");
    }
  }

  private async Task BrowseConfig()
  {
    TopLevel? topLevel = TopLevel.GetTopLevel(
      App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime
        desktop
        ? desktop.MainWindow
        : null);

    if (topLevel != null)
    {
      IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(
        new FilePickerOpenOptions
        {
          Title = "Select Configuration File",
          AllowMultiple = false,
          FileTypeFilter = new[]
          {
            new FilePickerFileType("TOML Files") {Patterns = new[] {"*.toml"}},
            new FilePickerFileType("All Files") {Patterns = new[] {"*"}},
          },
        });

      if (files.Count > 0)
      {
        await Dispatcher.UIThread.InvokeAsync(() => { ConfigFilePath = files[0].Path.LocalPath; });
        _ = LoadConfigurationFromFile();
      }
    }
  }

  private async Task LoadConfigurationFromFile()
  {
    try
    {
      if (!File.Exists(ConfigFilePath))
      {
        await Dispatcher.UIThread.InvokeAsync(() => { StatusMessage = "Config file not found"; });
        AddLogEntry("ERROR", "Config", $"Config file not found: {ConfigFilePath}");
        return;
      }

      // Stop all running workers before loading new config
      await StopAll();

      // Load config via daemon API
      await _daemonClient.LoadConfigAsync(ConfigFilePath);

      // Reload repositories from daemon
      await LoadRepositoriesFromDaemon();

      AddLogEntry("INFO", "Config", $"Loaded {Repositories.Count} repositories via daemon");
    }
    catch (Exception ex)
    {
      await Dispatcher.UIThread.InvokeAsync(() => { StatusMessage = $"Error loading config: {ex.Message}"; });
      AddLogEntry("ERROR", "Config", $"Error loading config: {ex.Message}");
    }
  }

  private async Task AddRepository()
  {
    TopLevel? topLevel = TopLevel.GetTopLevel(
      App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime
        desktop
        ? desktop.MainWindow
        : null);

    if (topLevel != null)
    {
      IReadOnlyList<IStorageFolder> folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
        new FolderPickerOpenOptions
        {
          Title = "Select Repository Folder",
          AllowMultiple = false,
        });

      if (folders.Count > 0)
      {
        string folderPath = folders[0].Path.LocalPath;
        string gitPath = Path.Combine(folderPath, ".git");

        if (!Directory.Exists(gitPath))
        {
          await Dispatcher.UIThread.InvokeAsync(() => { StatusMessage = "Selected folder is not a git repository"; });
          AddLogEntry("ERROR", "Repository", "Selected folder is not a git repository");
          return;
        }

        string repoName = new DirectoryInfo(folderPath).Name;

        try
        {
          RepositoryDto? repoDto = await _daemonClient.AddRepositoryAsync(repoName, folderPath);
          if (repoDto != null)
          {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
              RepositoryViewModel repoVm = new(repoDto.Name, repoDto.Path)
              {
                Id = repoDto.Id,
                IsRunning = repoDto.IsRunning,
                Status = repoDto.Status,
              };
              Repositories.Add(repoVm);
              StatusMessage = $"Added repository: {repoName}";
              UpdateComputedProperties();
            });

            AddLogEntry("INFO", "Repository", $"Added repository: {repoName}");
          }
        }
        catch (Exception ex)
        {
          AddLogEntry("ERROR", "Repository", $"Failed to add repository: {ex.Message}");
        }
      }
    }
  }

  private async Task StartAll()
  {
    try
    {
      StatusMessage = "Starting all repositories...";

      await _daemonClient.StartAllAsync();

      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        string now = DateTime.Now.ToString("HH:mm:ss");
        foreach (RepositoryViewModel repo in Repositories)
        {
          repo.IsRunning = true;
          repo.Status = "Running";
          repo.LastActivity = now;
        }

        UpdateComputedProperties();
      });

      StatusMessage = "All repositories started";
      AddLogEntry("INFO", "Daemon", "All repositories started");

      // Reconcile with daemon state to avoid stale UI if any repository failed to transition.
      await LoadRepositoriesFromDaemon();
    }
    catch (Exception ex)
    {
      IsRunning = false;
      AddLogEntry("ERROR", "Daemon", $"Failed to start all repositories: {ex.Message}");
    }
  }

  private async Task StopAll()
  {
    try
    {
      StatusMessage = "Stopping all repositories...";

      await _daemonClient.StopAllAsync();

      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        string now = DateTime.Now.ToString("HH:mm:ss");
        foreach (RepositoryViewModel repo in Repositories)
        {
          repo.IsRunning = false;
          repo.Status = "Stopped";
          repo.LastActivity = now;
        }

        UpdateComputedProperties();
      });

      StatusMessage = "All repositories stopped";
      AddLogEntry("INFO", "Daemon", "All repositories stopped");

      // Reconcile with daemon state to avoid stale UI if any repository failed to transition.
      await LoadRepositoriesFromDaemon();
    }
    catch (Exception ex)
    {
      AddLogEntry("ERROR", "Daemon", $"Failed to stop all repositories: {ex.Message}");
    }
  }

  private async Task StartRepository(RepositoryViewModel repo)
  {
    try
    {
      if (string.IsNullOrEmpty(repo.Id))
      {
        AddLogEntry("ERROR", repo.Name, "Repository ID is missing");
        return;
      }

      await _daemonClient.StartRepositoryAsync(repo.Id);
      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        repo.IsRunning = true;
        repo.Status = "Running";
        repo.LastActivity = DateTime.Now.ToString("HH:mm:ss");
        UpdateComputedProperties();
      });
      AddLogEntry("INFO", repo.Name, "Repository started");
    }
    catch (Exception ex)
    {
      AddLogEntry("ERROR", repo.Name, $"Failed to start: {ex.Message}");
    }
  }

  private async Task StopRepository(RepositoryViewModel repo)
  {
    try
    {
      if (string.IsNullOrEmpty(repo.Id))
      {
        AddLogEntry("ERROR", repo.Name, "Repository ID is missing");
        return;
      }

      await _daemonClient.StopRepositoryAsync(repo.Id);
      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        repo.IsRunning = false;
        repo.Status = "Stopped";
        repo.LastActivity = DateTime.Now.ToString("HH:mm:ss");
        UpdateComputedProperties();
      });
      AddLogEntry("INFO", repo.Name, "Repository stopped");
    }
    catch (Exception ex)
    {
      AddLogEntry("ERROR", repo.Name, $"Failed to stop: {ex.Message}");
    }
  }

  private async void RemoveRepository(RepositoryViewModel repo)
  {
    try
    {
      if (string.IsNullOrEmpty(repo.Id))
      {
        AddLogEntry("ERROR", repo.Name, "Repository ID is missing");
        return;
      }

      await _daemonClient.RemoveRepositoryAsync(repo.Id);

      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        Repositories.Remove(repo);
        UpdateComputedProperties();
      });

      AddLogEntry("INFO", repo.Name, "Repository removed");
    }
    catch (Exception ex)
    {
      AddLogEntry("ERROR", repo.Name, $"Failed to remove: {ex.Message}");
    }
  }

  private async Task CopyLog()
  {
    try
    {
      IClipboard? clipboard = TopLevel.GetTopLevel(
        App.Current?.ApplicationLifetime is
          Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
          ? desktop.MainWindow
          : null)?.Clipboard;

      if (clipboard != null)
      {
        await clipboard.SetTextAsync(LogText);
        AddLogEntry("INFO", "Application", "Log copied to clipboard");
      }
    }
    catch (Exception ex)
    {
      AddLogEntry("ERROR", "Application", $"Failed to copy log: {ex.Message}");
    }
  }

  private void ClearLog()
  {
    LogEntries.Clear();
    LogText = "";
    AddLogEntry("INFO", "Application", "Log cleared");
  }

  private async Task CheckStartupStatus()
  {
    try
    {
      IsStartupEnabled = await _startupManager.IsEnabledAsync();
    }
    catch (Exception ex)
    {
      AddLogEntry("WARNING", "Startup", $"Failed to check startup status: {ex.Message}");
    }
  }

  private async Task ToggleStartup()
  {
    try
    {
      if (!_startupManager.IsSupported)
      {
        StatusMessage = _startupManager.GetStatusMessage();
        AddLogEntry("ERROR", "Startup", _startupManager.GetStatusMessage());
        return;
      }

      if (IsStartupEnabled)
      {
        // Disabling startup - no config file needed
        bool success = await _startupManager.DisableAsync();
        if (success)
        {
          IsStartupEnabled = false;
          StatusMessage = "Startup on login disabled";
          AddLogEntry("INFO", "Startup", "Startup on login disabled successfully");
        }
        else
        {
          StatusMessage = "Failed to disable startup on login";
          AddLogEntry("ERROR", "Startup", "Failed to disable startup on login");
        }
      }
      else
      {
        // Enabling startup - config file is required
        if (string.IsNullOrEmpty(ConfigFilePath) || !File.Exists(ConfigFilePath))
        {
          StatusMessage = "Please load a configuration file first";
          AddLogEntry("ERROR", "Startup", "Cannot set up startup without a valid configuration file");
          return;
        }

        bool success = await _startupManager.EnableAsync(ConfigFilePath);
        if (success)
        {
          IsStartupEnabled = true;
          StatusMessage = "Startup on login enabled";
          AddLogEntry("INFO", "Startup", "Startup on login enabled successfully");
        }
        else
        {
          StatusMessage = "Failed to enable startup on login";
          AddLogEntry("ERROR", "Startup", "Failed to enable startup on login");
        }
      }
    }
    catch (Exception ex)
    {
      StatusMessage = $"Startup toggle failed: {ex.Message}";
      AddLogEntry("ERROR", "Startup", $"Startup toggle failed: {ex.Message}");
    }
  }

  private void UpdateComputedProperties()
  {
    // This method will be called from UI thread or marshaled to UI thread
    if (Dispatcher.UIThread.CheckAccess())
    {
      TotalRepositories = Repositories.Count;
      ActiveRepositories = Repositories.Count(r => r.IsRunning);
      IsRunning = ActiveRepositories > 0;
      CanStartAll = TotalRepositories > 0 && ActiveRepositories < TotalRepositories;
      CanStopAll = ActiveRepositories > 0;

      // Notify commands that CanExecute might have changed
      ((ThreadSafeCommand) StartAllCommand).RaiseCanExecuteChanged();
      ((ThreadSafeCommand) StopAllCommand).RaiseCanExecuteChanged();
    }
    else
    {
      Dispatcher.UIThread.InvokeAsync(() =>
      {
        TotalRepositories = Repositories.Count;
        ActiveRepositories = Repositories.Count(r => r.IsRunning);
        IsRunning = ActiveRepositories > 0;
        CanStartAll = TotalRepositories > 0 && ActiveRepositories < TotalRepositories;
        CanStopAll = ActiveRepositories > 0;

        // Notify commands that CanExecute might have changed
        ((ThreadSafeCommand) StartAllCommand).RaiseCanExecuteChanged();
        ((ThreadSafeCommand) StopAllCommand).RaiseCanExecuteChanged();
      });
    }
  }

  public void AddLogEntry(string level, string repository, string message)
  {
    LogEntryViewModel entry = new()
    {
      Timestamp = DateTime.Now,
      Level = level,
      Repository = repository,
      Message = message,
    };

    // Ensure this is always called on UI thread
    if (Dispatcher.UIThread.CheckAccess())
    {
      AddLogEntryToCollection(entry);
    }
    else
    {
      Dispatcher.UIThread.InvokeAsync(() => AddLogEntryToCollection(entry));
    }
  }

  private void AddLogEntryToCollection(LogEntryViewModel entry)
  {
    LogEntries.Insert(0, entry);

    // Keep only last 1000 entries
    while (LogEntries.Count > 1000)
    {
      LogEntries.RemoveAt(LogEntries.Count - 1);
    }

    // Rebuild plain-text log for the TextBox (newest first)
    RebuildLogText();
  }

  private void RebuildLogText()
  {
    StringBuilder sb = new();
    foreach (LogEntryViewModel e in LogEntries)
    {
      sb.AppendLine($"{e.Timestamp:HH:mm:ss} {e.Level,-7} {e.Repository,-15} {e.Message}");
    }

    LogText = sb.ToString();
  }

  private async void SaveConfig()
  {
    try
    {
      if (string.IsNullOrEmpty(ConfigFilePath))
      {
        // If no config file is loaded, we need a path first
        AddLogEntry("ERROR", "Config", "No configuration file path set. Please use 'Open Configuration' first.");
        return;
      }

      // Save config via daemon API
      await _daemonClient.SaveConfigAsync(ConfigFilePath);
      AddLogEntry("INFO", "Config", $"Configuration saved to {ConfigFilePath} via daemon");
    }
    catch (Exception ex)
    {
      AddLogEntry("ERROR", "Config", $"Failed to save configuration: {ex.Message}");
    }
  }

  private void ShowAbout()
  {
    try
    {
      string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
      string aboutMessage =
        $"GitAutoSync v{version} - A cross-platform Git repository synchronization tool. Built with Avalonia UI and .NET 9";

      AddLogEntry("INFO", "Application", "About dialog requested");
      AddLogEntry("INFO", "Application", aboutMessage);
    }
    catch (Exception ex)
    {
      AddLogEntry("ERROR", "Application", $"Failed to show about dialog: {ex.Message}");
    }
  }

  private async Task AutoStartMonitoringAsync()
  {
    try
    {
      AddLogEntry("INFO", "AutoStart", "Auto-start monitoring requested...");

      // Check if UI is ready first
      if (!_uiReady)
      {
        AddLogEntry("WARNING", "AutoStart", "UI not ready yet, auto-start may be delayed");
      }

      // First check if repositories are already loaded
      if (Repositories.Count > 0)
      {
        AddLogEntry("INFO", "AutoStart", $"Found {Repositories.Count} repositories already loaded");

        if (CanStartAll && !IsRunning)
        {
          AddLogEntry("INFO", "AutoStart", $"Auto-starting monitoring for {Repositories.Count} repositories");
          await StartAll();
          return;
        }
        else if (IsRunning)
        {
          AddLogEntry("INFO", "AutoStart", "Monitoring already running, auto-start not needed");
          return;
        }
        else
        {
          AddLogEntry(
            "WARNING",
            "AutoStart",
            "Cannot start monitoring - repositories loaded but start conditions not met");
          return;
        }
      }

      // Wait for config to load by checking repository count periodically
      AddLogEntry("INFO", "AutoStart", "Waiting for configuration to load...");
      int attempts = 0;
      const int maxAttempts = 30; // Wait up to 30 seconds

      while (attempts < maxAttempts)
      {
        await Task.Delay(1000); // Wait 1 second
        attempts++;

        if (Repositories.Count > 0)
        {
          AddLogEntry(
            "INFO",
            "AutoStart",
            $"Configuration loaded with {Repositories.Count} repositories after {attempts} seconds");

          // Config loaded, now check if we can start
          if (CanStartAll && !IsRunning)
          {
            AddLogEntry("INFO", "AutoStart", $"Auto-starting monitoring for {Repositories.Count} repositories");
            await StartAll();
            return;
          }
          else if (IsRunning)
          {
            AddLogEntry("INFO", "AutoStart", "Monitoring already running, auto-start not needed");
            return;
          }
        }
      }

      // Timeout reached
      AddLogEntry("WARNING", "AutoStart", "Auto-start timeout: No repositories loaded or unable to start monitoring");
    }
    catch (Exception ex)
    {
      AddLogEntry("ERROR", "AutoStart", $"Failed to auto-start monitoring: {ex.Message}");
    }
  }

  public void OnUIReady()
  {
    _uiReady = true;

    AddLogEntry("INFO", "Application", "UI ready");

    // If auto-start was requested, start monitoring
    if (_shouldAutoStart)
    {
      AddLogEntry("INFO", "Application", "UI ready, checking for auto-start...");
      _ = Task.Run(async () =>
      {
        // Give a small delay to ensure everything is fully initialized
        await Task.Delay(500);
        await AutoStartMonitoringAsync();
      });
    }
  }

  public void Dispose()
  {
    _webSocketClient.EventReceived -= OnDaemonEvent;
    _webSocketClient.Dispose();
  }
}