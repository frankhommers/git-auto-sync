namespace GitAutoSync.Daemon.Models;

public class RepositoryInfo
{
  public required string Id { get; init; }
  public required string Name { get; init; }
  public required string Path { get; init; }
  public bool IsRunning { get; set; }
  public DateTime? LastActivity { get; set; }
  public string Status { get; set; } = "Stopped";
}