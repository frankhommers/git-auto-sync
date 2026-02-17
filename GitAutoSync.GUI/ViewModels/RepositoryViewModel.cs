using ReactiveUI;
using Avalonia.Threading;

namespace GitAutoSync.GUI.ViewModels;

public class RepositoryViewModel : ViewModelBase
{
  private bool _isRunning;
  private string _status = "Stopped";
  private string _lastActivity = "";
  private bool _canStart = true;
  private string _statusColor = "#718096";

  public string Id { get; set; } = string.Empty;
  public string Name { get; }
  public string Path { get; }

  public bool IsRunning
  {
    get => _isRunning;
    set
    {
      if (Dispatcher.UIThread.CheckAccess())
      {
        this.RaiseAndSetIfChanged(ref _isRunning, value);
        UpdateComputedProperties();
      }
      else
      {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
          this.RaiseAndSetIfChanged(ref _isRunning, value);
          UpdateComputedProperties();
        });
      }
    }
  }

  public string Status
  {
    get => _status;
    set
    {
      if (Dispatcher.UIThread.CheckAccess())
      {
        this.RaiseAndSetIfChanged(ref _status, value);
        UpdateComputedProperties();
      }
      else
      {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
          this.RaiseAndSetIfChanged(ref _status, value);
          UpdateComputedProperties();
        });
      }
    }
  }

  public string LastActivity
  {
    get => _lastActivity;
    set
    {
      if (Dispatcher.UIThread.CheckAccess())
      {
        this.RaiseAndSetIfChanged(ref _lastActivity, value);
      }
      else
      {
        Dispatcher.UIThread.InvokeAsync(() => { this.RaiseAndSetIfChanged(ref _lastActivity, value); });
      }
    }
  }

  public bool CanStart
  {
    get => _canStart;
    private set
    {
      if (Dispatcher.UIThread.CheckAccess())
      {
        this.RaiseAndSetIfChanged(ref _canStart, value);
      }
      else
      {
        Dispatcher.UIThread.InvokeAsync(() => this.RaiseAndSetIfChanged(ref _canStart, value));
      }
    }
  }

  public string StatusColor
  {
    get => _statusColor;
    private set
    {
      if (Dispatcher.UIThread.CheckAccess())
      {
        this.RaiseAndSetIfChanged(ref _statusColor, value);
      }
      else
      {
        Dispatcher.UIThread.InvokeAsync(() => this.RaiseAndSetIfChanged(ref _statusColor, value));
      }
    }
  }

  private void UpdateComputedProperties()
  {
    if (Dispatcher.UIThread.CheckAccess())
    {
      CanStart = !IsRunning;
      StatusColor = Status switch
      {
        "Running" => "#48BB78",
        "Error" => "#F56565",
        "Stopped" => "#718096",
        _ => "#718096",
      };
    }
    else
    {
      Dispatcher.UIThread.InvokeAsync(() =>
      {
        CanStart = !IsRunning;
        StatusColor = Status switch
        {
          "Running" => "#48BB78",
          "Error" => "#F56565",
          "Stopped" => "#718096",
          _ => "#718096",
        };
      });
    }
  }

  public RepositoryViewModel(string name, string path)
  {
    Name = name;
    Path = path;
    UpdateComputedProperties();
  }
}