namespace GitAutoSync.GUI.Models;

public class RepositoryDto
{
  public required string Id { get; init; }
  public required string Name { get; init; }
  public required string Path { get; init; }
  public bool IsRunning { get; set; }
  public DateTime? LastActivity { get; set; }
  public string Status { get; set; } = "Stopped";
}

public record AddRepositoryRequest(string Name, string Path);