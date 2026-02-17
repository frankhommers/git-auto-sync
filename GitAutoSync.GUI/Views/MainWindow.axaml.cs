using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using GitAutoSync.GUI.ViewModels;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using GitAutoSync.GUI.Services;
using ReactiveUI;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Timer = System.Timers.Timer;

namespace GitAutoSync.GUI.Views;

public partial class MainWindow : Window
{
  private NativeMenuItem? _startupMenuItem;

  public MainWindow()
  {
    InitializeComponent();
    ShowInTaskbar = true;
    MacOsActivationPolicy.SetRegular();

    // Set window icon programmatically
    try
    {
      Stream iconAsset = AssetLoader.Open(new Uri("avares://GitAutoSync.GUI/Assets/app-icon.png"));
      Icon = new WindowIcon(new Bitmap(iconAsset));
    }
    catch
    {
      // Fallback if icon loading fails
    }

    // Set up native menu
    SetupNativeMenu();

    // Hook into multiple events to ensure UI initialization happens regardless of window state
    Opened += OnWindowOpened;
    Activated += OnWindowActivated;
    Loaded += OnWindowLoaded;

    // Monitor window state changes to refresh UI when restored from minimized
    PropertyChanged += OnWindowPropertyChanged;

    // Also use a timer as a fallback to ensure initialization happens even if events don't fire
    Timer initTimer = new(1000); // 1 second delay
    initTimer.Elapsed += (sender, e) =>
    {
      initTimer.Stop();
      initTimer.Dispose();
      Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => EnsureUIInitialized());
    };
    initTimer.Start();
  }

  private bool _uiInitialized = false;

  private void OnWindowOpened(object? sender, EventArgs e)
  {
    // Notify the ViewModel that the UI is fully loaded and ready
    EnsureUIInitialized();
  }

  private void OnWindowActivated(object? sender, EventArgs e)
  {
    // Also try to initialize when window is activated (in case Opened didn't fire)
    EnsureUIInitialized();
  }

  private void OnWindowLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    // Initialize when window is loaded
    EnsureUIInitialized();
  }

  private void EnsureUIInitialized()
  {
    if (_uiInitialized)
    {
      return;
    }

    _uiInitialized = true;

    if (DataContext is MainWindowViewModel viewModel)
    {
      System.Diagnostics.Debug.WriteLine("UI initialization triggered - notifying ViewModel");
      viewModel.OnUIReady();
    }
    else
    {
      System.Diagnostics.Debug.WriteLine("UI initialization triggered but DataContext is not MainWindowViewModel");
      // If DataContext isn't set yet, wait a bit and try again
      Timer retryTimer = new(500);
      retryTimer.Elapsed += (sender, e) =>
      {
        retryTimer.Stop();
        retryTimer.Dispose();
        if (!_uiInitialized) // Reset flag to allow retry
        {
          _uiInitialized = false;
          Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => EnsureUIInitialized());
        }
      };
      retryTimer.Start();
    }
  }

  private void SetupNativeMenu()
  {
    try
    {
      // Only set up native menu on platforms that support it properly
      if (IsNativeMenuSupported())
      {
        CreateNativeMenu();
      }
      else
      {
        // Fallback for platforms where native menu might not work well
        CreateFallbackMenu();
      }
    }
    catch (Exception ex)
    {
      // If native menu setup fails, we'll just continue without it
      System.Diagnostics.Debug.WriteLine($"Failed to setup native menu: {ex.Message}");
    }
  }

  private bool IsNativeMenuSupported()
  {
    // Native menus work best on macOS, reasonably on Windows, and vary on Linux
    return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
           RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
  }

  private void CreateNativeMenu()
  {
    List<NativeMenuItem> menuItems = new();

    // Create File menu
    NativeMenuItem fileMenu = new("File")
    {
      Menu = new NativeMenu
      {
        CreateMenuItem("Open Configuration...", "Ctrl+O", vm => vm.BrowseConfigCommand),
        CreateMenuItem("Save Configuration", "Ctrl+S", vm => vm.SaveConfigCommand),
      },
    };

    // Add Exit menu item only for non-macOS platforms
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
      fileMenu.Menu.Add(new NativeMenuItemSeparator());
      fileMenu.Menu.Add(CreateExitMenuItem());
    }

    menuItems.Add(fileMenu);

    // Create Preferences/Options menu with startup toggle
    _startupMenuItem = new NativeMenuItem("Start at Login")
    {
      Command = ReactiveCommand.Create(() =>
      {
        if (DataContext is MainWindowViewModel vm)
        {
          vm.ToggleStartupCommand.Execute(null);
        }
      }),
      ToggleType = NativeMenuItemToggleType.CheckBox,
    };

    // Use "Preferences" on macOS, "Options" on other platforms
    string prefsMenuName = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "Preferences" : "Options";
    NativeMenuItem prefsMenu = new(prefsMenuName)
    {
      Menu = new NativeMenu {_startupMenuItem},
    };
    menuItems.Add(prefsMenu);

    // Create Help menu (About will be handled automatically by macOS)
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
      NativeMenuItem helpMenu = new("Help")
      {
        Menu = new NativeMenu
        {
          CreateMenuItem("About GitAutoSync", null, vm => vm.ShowAboutCommand),
        },
      };
      menuItems.Add(helpMenu);
    }

    // Create menu bar
    NativeMenu menuBar = new();
    foreach (NativeMenuItem item in menuItems)
    {
      menuBar.Add(item);
    }

    // Set the native menu
    NativeMenu.SetMenu(this, menuBar);

    // Set up data context binding
    SetupDataContextBinding();
  }

  private void CreateFallbackMenu()
  {
    // For platforms where native menu doesn't work well,
    // we could add a menu bar to the window content or use other UI patterns
    // For now, we'll just skip the menu on these platforms
    System.Diagnostics.Debug.WriteLine("Native menu not supported on this platform, running without menu");
  }

  private NativeMenuItem CreateMenuItem(
    string header,
    string? gesture,
    Func<MainWindowViewModel, System.Windows.Input.ICommand> commandSelector)
  {
    NativeMenuItem menuItem = new(header)
    {
      Command = ReactiveCommand.Create(() =>
      {
        if (DataContext is MainWindowViewModel vm)
        {
          ICommand? command = commandSelector(vm);
          if (command?.CanExecute(null) == true)
          {
            command.Execute(null);
          }
        }
      }),
    };

    if (!string.IsNullOrEmpty(gesture))
    {
      try
      {
        menuItem.Gesture = KeyGesture.Parse(gesture);
      }
      catch
      {
        // Ignore gesture parsing errors
      }
    }

    return menuItem;
  }

  private NativeMenuItem CreateExitMenuItem()
  {
    NativeMenuItem exitItem = new("Exit")
    {
      Command = ReactiveCommand.Create(() =>
      {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
          desktop.Shutdown();
        }
      }),
    };

    // Set platform-appropriate exit gesture
    try
    {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      {
        exitItem.Gesture = KeyGesture.Parse("Cmd+Q");
      }
      else
      {
        exitItem.Gesture = KeyGesture.Parse("Alt+F4");
      }
    }
    catch
    {
      // Ignore gesture parsing errors
    }

    return exitItem;
  }

  private void SetupDataContextBinding()
  {
    // Update menu items when DataContext changes
    DataContextChanged += OnDataContextChanged;

    // Set initial state if DataContext is already set
    OnDataContextChanged(this, EventArgs.Empty);
  }

  private void OnDataContextChanged(object? sender, EventArgs e)
  {
    if (DataContext is MainWindowViewModel vm)
    {
      // Subscribe to property changes
      vm.PropertyChanged += OnViewModelPropertyChanged;

      // Set initial startup menu state
      UpdateStartupMenuState(vm.IsStartupEnabled);
    }
  }

  private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (sender is MainWindowViewModel vm && e.PropertyName == nameof(MainWindowViewModel.IsStartupEnabled))
    {
      UpdateStartupMenuState(vm.IsStartupEnabled);
    }
  }

  private void UpdateStartupMenuState(bool isEnabled)
  {
    if (_startupMenuItem != null)
    {
      _startupMenuItem.IsChecked = isEnabled;
    }
  }

  private void OnWindowPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
  {
    // When window state changes, especially when restored from minimized, refresh the UI
    if (e.Property.Name == nameof(WindowState))
    {
      if (WindowState != WindowState.Minimized)
      {
        // Window was restored from minimized, ensure UI is properly initialized
        EnsureUIInitialized();

        // Force a refresh of the DataContext bindings
        if (DataContext is MainWindowViewModel viewModel)
        {
          // Trigger property change notifications to refresh bindings
          Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
          {
            viewModel.RaisePropertyChanged(nameof(MainWindowViewModel.Repositories));
            viewModel.RaisePropertyChanged(nameof(MainWindowViewModel.TotalRepositories));
            viewModel.RaisePropertyChanged(nameof(MainWindowViewModel.ActiveRepositories));
            viewModel.RaisePropertyChanged(nameof(MainWindowViewModel.StatusMessage));
            viewModel.RaisePropertyChanged(nameof(MainWindowViewModel.CanStartAll));
            viewModel.RaisePropertyChanged(nameof(MainWindowViewModel.CanStopAll));
          });
        }
      }
    }
  }

  private bool _isQuitting;

  public void ForceClose()
  {
    _isQuitting = true;
    Close();
  }

  protected override void OnClosing(WindowClosingEventArgs e)
  {
    if (!_isQuitting)
    {
      // Hide window instead of closing (minimize to tray)
      e.Cancel = true;
      ShowInTaskbar = false;
      Hide();
      MacOsActivationPolicy.SetAccessory();
    }

    base.OnClosing(e);
  }
}