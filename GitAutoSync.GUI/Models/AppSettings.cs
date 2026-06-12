namespace GitAutoSync.GUI.Models;

public class AppSettings
{
  public string? ThemeMode { get; set; }
  public List<OpenWithApp> OpenWithApps { get; set; } = new();
}

public class OpenWithApp
{
  public string Name { get; set; } = "";
  public string Command { get; set; } = "";
}
