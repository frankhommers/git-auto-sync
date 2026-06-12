using System.Text.Json;
using GitAutoSync.GUI.Models;

namespace GitAutoSync.GUI.Services;

/// <summary>
/// Loads and saves GUI application settings (~/.config/GitAutoSync/settings.json).
/// The old format was a flat string dictionary ({"themeMode":"Dark"}); since its
/// properties are a subset of <see cref="AppSettings"/>, both formats parse the same way.
/// </summary>
public static class AppSettingsStore
{
  public const int MaxOpenWithApps = 5;

  public static AppSettings Parse(string json)
  {
    AppSettings? settings = null;
    try
    {
      settings = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppSettings);
    }
    catch (JsonException)
    {
      // Invalid JSON falls through to defaults.
    }

    settings ??= new AppSettings();
    settings.OpenWithApps ??= new List<OpenWithApp>();

    if (settings.OpenWithApps.Count > MaxOpenWithApps)
    {
      settings.OpenWithApps = settings.OpenWithApps.Take(MaxOpenWithApps).ToList();
    }

    return settings;
  }

  public static string Serialize(AppSettings settings)
  {
    return JsonSerializer.Serialize(settings, AppJsonContext.Default.AppSettings);
  }

  public static AppSettings Load(string path)
  {
    if (!File.Exists(path))
    {
      return new AppSettings();
    }

    return Parse(File.ReadAllText(path));
  }

  public static void Save(string path, AppSettings settings)
  {
    string? directory = Path.GetDirectoryName(path);
    if (!string.IsNullOrWhiteSpace(directory))
    {
      Directory.CreateDirectory(directory);
    }

    File.WriteAllText(path, Serialize(settings));
  }
}
