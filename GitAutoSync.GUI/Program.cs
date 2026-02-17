using System;
using System.IO;
using Avalonia;
using GitAutoSync.GUI;
using GitAutoSync.GUI.Services;

namespace GitAutoSync.GUI;

internal class Program
{
  public static string[] CommandLineArgs { get; private set; } = Array.Empty<string>();
  private static FileStream? _singleInstanceLock;

  // Initialization code. Don't use any Avalonia, third-party APIs or any
  // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
  // yet and stuff might break.
  [STAThread]
  public static void Main(string[] args)
  {
    CommandLineArgs = args;

    string lockPath = Path.Combine(Path.GetTempPath(), "git-auto-sync-gui.lock");
    try
    {
      _singleInstanceLock = new FileStream(
        lockPath,
        FileMode.OpenOrCreate,
        FileAccess.ReadWrite,
        FileShare.None);
    }
    catch (IOException)
    {
      Console.WriteLine("Git Auto Sync is already running.");
      return;
    }

    // Ensure normal app activation on startup (dock/window visible on macOS).
    MacOsActivationPolicy.SetRegular();

    BuildAvaloniaApp()
      .StartWithClassicDesktopLifetime(args);

    _singleInstanceLock.Dispose();
  }

  // Avalonia configuration, don't remove; also used by visual designer.
  public static AppBuilder BuildAvaloniaApp()
  {
    return AppBuilder.Configure<App>()
      .UsePlatformDetect()
      .WithInterFont()
      .LogToTrace();
  }
}