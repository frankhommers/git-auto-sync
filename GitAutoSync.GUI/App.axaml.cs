using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GitAutoSync.GUI.Services;
using GitAutoSync.GUI.ViewModels;
using GitAutoSync.GUI.Views;

namespace GitAutoSync.GUI;

public partial class App : Application
{
  private TrayIconManager? _trayIconManager;
  private DaemonLifecycle? _daemonLifecycle;

  public override void Initialize()
  {
    AvaloniaXamlLoader.Load(this);
    Name = "Git Auto Sync";
  }

  public override async void OnFrameworkInitializationCompleted()
  {
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      // Parse command line arguments
      string? configFilePath = null;
      bool autoStart = false;

      string[] args = Program.CommandLineArgs;
      for (int i = 0; i < args.Length; i++)
      {
        switch (args[i])
        {
          case "--config-file" when i + 1 < args.Length:
            configFilePath = args[i + 1];
            i++; // Skip next argument as it's the value
            break;
          case "--auto-start":
            autoStart = true;
            break;
        }
      }

      // Initialize daemon services
      DaemonClient daemonClient = new();
      DaemonWebSocketClient webSocketClient = new();

      // Ensure daemon is running BEFORE creating the ViewModel
      _daemonLifecycle = new DaemonLifecycle(daemonClient);

      AppDomain.CurrentDomain.ProcessExit += (_, _) =>
      {
        _trayIconManager?.Dispose();
        _daemonLifecycle?.Dispose();
      };

      MainWindowViewModel viewModel = new(daemonClient, webSocketClient, configFilePath, autoStart);

      desktop.MainWindow = new MainWindow
      {
        DataContext = viewModel,
      };

      // Initialize tray icon
      _trayIconManager = new TrayIconManager();
      _trayIconManager.Initialize(desktop.MainWindow, daemonClient);

      // Clean up on application exit
      desktop.ShutdownRequested += (_, _) =>
      {
        if (desktop.MainWindow is MainWindow mw)
        {
          mw.ForceClose();
        }
      };

      desktop.Exit += (_, _) =>
      {
        _trayIconManager?.Dispose();
        viewModel.Dispose();
        _daemonLifecycle?.Dispose();
      };

      // Start daemon and connect ViewModel AFTER window is shown
      _ = InitializeDaemonAsync(viewModel);
    }

    base.OnFrameworkInitializationCompleted();
  }

  private async Task InitializeDaemonAsync(MainWindowViewModel viewModel)
  {
    bool daemonReady = await _daemonLifecycle!.EnsureDaemonRunningAsync();
    viewModel.OnDaemonReady(daemonReady);
  }

  private async void AboutMenuItem_OnClick(object? sender, EventArgs e)
  {
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
        desktop.MainWindow != null)
    {
      AboutWindow aboutWindow = new();
      await aboutWindow.ShowDialog(desktop.MainWindow);
    }
  }

  private void QuitMenuItem_OnClick(object? sender, EventArgs e)
  {
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      if (desktop.MainWindow is MainWindow mw)
      {
        mw.ForceClose();
      }

      desktop.Shutdown();
    }
  }
}
