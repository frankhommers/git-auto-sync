using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using System.Runtime.InteropServices;

namespace GitAutoSync.GUI.Services;

public class TrayIconManager
{
  private TrayIcon? _trayIcon;
  private Window? _mainWindow;
  private DaemonClient? _daemonClient;

  public void Initialize(Window mainWindow, DaemonClient daemonClient)
  {
    _mainWindow = mainWindow;
    _daemonClient = daemonClient;

    WindowIcon icon;
    try
    {
      icon = new WindowIcon(AssetLoader.Open(new Uri("avares://GitAutoSync.GUI/Assets/icon-tray.png")));
    }
    catch
    {
      icon = new WindowIcon(AssetLoader.Open(new Uri("avares://GitAutoSync.GUI/Assets/icon.png")));
    }

    _trayIcon = new TrayIcon
    {
      Icon = icon,
      ToolTipText = "Git Auto Sync",
      IsVisible = true,
    };

    NativeMenu menu = new();

    NativeMenuItem showItem = new("Show Window");
    showItem.Click += (_, _) => ShowWindow();
    menu.Items.Add(showItem);

    NativeMenuItem hideItem = new("Hide Window");
    hideItem.Click += (_, _) => HideWindow();
    menu.Items.Add(hideItem);

    menu.Items.Add(new NativeMenuItemSeparator());

    NativeMenuItem startAllItem = new("Start All");
    startAllItem.Click += async (_, _) => await OnStartAllClicked();
    menu.Items.Add(startAllItem);

    NativeMenuItem stopAllItem = new("Stop All");
    stopAllItem.Click += async (_, _) => await OnStopAllClicked();
    menu.Items.Add(stopAllItem);

    menu.Items.Add(new NativeMenuItemSeparator());

    NativeMenuItem quitItem = new("Quit");
    quitItem.Click += (_, _) => Quit();
    menu.Items.Add(quitItem);

    _trayIcon.Menu = menu;

    // Double-click to show window
    _trayIcon.Clicked += (_, _) => ShowWindow();
  }

  private void ShowWindow()
  {
    if (_mainWindow != null)
    {
      MacOsActivationPolicy.SetRegular();
      _mainWindow.ShowInTaskbar = true;
      _mainWindow.Show();
      _mainWindow.WindowState = WindowState.Normal;
      _mainWindow.Activate();
    }
  }

  private void HideWindow()
  {
    if (_mainWindow != null)
    {
      _mainWindow.ShowInTaskbar = false;
      _mainWindow.Hide();
      MacOsActivationPolicy.SetAccessory();
    }
  }

  private async Task OnStartAllClicked()
  {
    try
    {
      if (_daemonClient != null)
      {
        await _daemonClient.StartAllAsync();
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Failed to start all repositories: {ex.Message}");
    }
  }

  private async Task OnStopAllClicked()
  {
    try
    {
      if (_daemonClient != null)
      {
        await _daemonClient.StopAllAsync();
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Failed to stop all repositories: {ex.Message}");
    }
  }

  private void Quit()
  {
    if (_mainWindow is Views.MainWindow mw)
    {
      mw.ForceClose();
    }

    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      desktop.Shutdown();
    }
  }

  public void Dispose()
  {
    if (_trayIcon != null)
    {
      _trayIcon.IsVisible = false;
      _trayIcon = null;
    }
  }
}