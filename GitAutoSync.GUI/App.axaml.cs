using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System.Reflection;
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
      string appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

      // Create a simple about window
      Window aboutWindow = new()
      {
        Title = "About Git Auto Sync",
        Width = 400,
        Height = 250,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        CanResize = false,
        ShowInTaskbar = false,
        Content = new StackPanel
        {
          Margin = new Thickness(20),
          Children =
          {
            new TextBlock
            {
              Text = "Git Auto Sync",
              FontSize = 20,
              FontWeight = FontWeight.Bold,
              HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
              Margin = new Thickness(0, 0, 0, 10),
            },
            new TextBlock
            {
              Text = "Automatically sync your git repositories",
              FontSize = 14,
              HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
              Margin = new Thickness(0, 0, 0, 20),
            },
            new TextBlock
            {
              Text = $"Version {appVersion}",
              FontSize = 12,
              HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
              Margin = new Thickness(0, 0, 0, 5),
            },
            new TextBlock
            {
              Text = "© 2025 Frank Hommers",
              FontSize = 12,
              HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
              Margin = new Thickness(0, 0, 0, 20),
            },
            new Button
            {
              Content = "OK",
              HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
              Width = 80,
            },
          },
        },
      };

      // Add click handler to close the window
      if (aboutWindow.Content is StackPanel panel &&
          panel.Children.LastOrDefault() is Button okButton)
      {
        okButton.Click += (_, _) => aboutWindow.Close();
      }

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
