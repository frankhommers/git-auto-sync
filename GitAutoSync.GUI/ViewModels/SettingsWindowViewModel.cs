using System.Collections.ObjectModel;
using System.Windows.Input;
using ReactiveUI;
using GitAutoSync.GUI.Commands;
using GitAutoSync.GUI.Models;
using GitAutoSync.GUI.Services;

namespace GitAutoSync.GUI.ViewModels;

public class SettingsWindowViewModel : ViewModelBase
{
  public ObservableCollection<OpenWithAppItemViewModel> Apps { get; } = new();

  public ICommand RemoveAppCommand { get; }

  public int MaxApps => AppSettingsStore.MaxOpenWithApps;

  public bool CanAddApp => Apps.Count < AppSettingsStore.MaxOpenWithApps;

  public string AppCountText => $"{Apps.Count} of {AppSettingsStore.MaxOpenWithApps} applications configured";

  public SettingsWindowViewModel(IEnumerable<OpenWithApp> apps)
  {
    foreach (OpenWithApp app in apps.Take(AppSettingsStore.MaxOpenWithApps))
    {
      Apps.Add(new OpenWithAppItemViewModel {Name = app.Name, Command = app.Command});
    }

    RemoveAppCommand = new ThreadSafeCommand<OpenWithAppItemViewModel>(item =>
    {
      if (item != null)
      {
        Apps.Remove(item);
      }
    });

    Apps.CollectionChanged += (_, _) =>
    {
      this.RaisePropertyChanged(nameof(CanAddApp));
      this.RaisePropertyChanged(nameof(AppCountText));
    };
  }

  public void AddApplication(string applicationPath)
  {
    if (!CanAddApp)
    {
      return;
    }

    Apps.Add(new OpenWithAppItemViewModel
    {
      Name = OpenWithCommandParser.DeriveAppName(applicationPath),
      Command = OpenWithCommandParser.BuildDefaultCommand(applicationPath),
    });
  }

  public List<OpenWithApp> ToOpenWithApps()
  {
    return Apps
      .Where(item => !string.IsNullOrWhiteSpace(item.Name) && !string.IsNullOrWhiteSpace(item.Command))
      .Select(item => new OpenWithApp {Name = item.Name.Trim(), Command = item.Command.Trim()})
      .ToList();
  }
}

public class OpenWithAppItemViewModel : ViewModelBase
{
  private string _name = "";
  private string _command = "";

  public string Name
  {
    get => _name;
    set => this.RaiseAndSetIfChanged(ref _name, value);
  }

  public string Command
  {
    get => _command;
    set => this.RaiseAndSetIfChanged(ref _command, value);
  }
}
