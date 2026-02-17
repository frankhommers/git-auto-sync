using ReactiveUI;

namespace GitAutoSync.GUI.ViewModels;

public class LogEntryViewModel : ViewModelBase
{
  public DateTime Timestamp { get; set; }
  public string Level { get; set; } = "";
  public string Repository { get; set; } = "";
  public string Message { get; set; } = "";

  public string LevelColor => Level switch
  {
    "ERROR" => "#F56565",
    "WARN" => "#ED8936",
    "INFO" => "#48BB78",
    "DEBUG" => "#4299E1",
    _ => "#2D3748",
  };
}