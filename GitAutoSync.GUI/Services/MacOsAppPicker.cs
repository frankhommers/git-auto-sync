using System.Diagnostics;

namespace GitAutoSync.GUI.Services;

/// <summary>
/// Shows the native macOS "Choose Application" dialog via AppleScript.
/// Needed because Avalonia's OpenFilePickerAsync silently drops .app bundles:
/// they are directories on disk, so the managed side wraps them as IStorageFolder
/// and OpenFileDialog filters them out with OfType&lt;IStorageFile&gt;().
/// </summary>
public static class MacOsAppPicker
{
  public static async Task<string?> PickApplicationAsync()
  {
    try
    {
      ProcessStartInfo startInfo = new("osascript")
      {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
      };
      // Note: "choose application" is NOT used here: its list comes from a legacy
      // Launch Services mechanism that often misses third-party apps (e.g. Fork).
      // "choose file" with the application-bundle UTI is a real NSOpenPanel where
      // .app bundles are selectable.
      startInfo.ArgumentList.Add("-e");
      startInfo.ArgumentList.Add(
        "POSIX path of (choose file of type {\"com.apple.application-bundle\"} " +
        "default location (path to applications folder) " +
        "with prompt \"Choose an application to open repositories with:\")");

      using Process? process = Process.Start(startInfo);
      if (process is null)
      {
        return null;
      }

      string output = await process.StandardOutput.ReadToEndAsync();
      await process.WaitForExitAsync();

      if (process.ExitCode != 0)
      {
        // Non-zero exit code also covers "User canceled. (-128)".
        return null;
      }

      string path = output.Trim();
      return string.IsNullOrEmpty(path) ? null : path;
    }
    catch
    {
      return null;
    }
  }
}
