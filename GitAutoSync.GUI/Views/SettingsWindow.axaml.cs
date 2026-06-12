using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using GitAutoSync.GUI.Models;
using GitAutoSync.GUI.Services;
using GitAutoSync.GUI.ViewModels;

namespace GitAutoSync.GUI.Views;

public partial class SettingsWindow : Window
{
  private readonly SettingsWindowViewModel _viewModel;

  public SettingsWindow() : this(Enumerable.Empty<OpenWithApp>())
  {
  }

  public SettingsWindow(IEnumerable<OpenWithApp> openWithApps)
  {
    InitializeComponent();
    _viewModel = new SettingsWindowViewModel(openWithApps);
    DataContext = _viewModel;
  }

  private async void AddApplicationButton_OnClick(object? sender, RoutedEventArgs e)
  {
    string? applicationPath;

    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
      // Avalonia's file picker silently drops .app bundles (returned as folders),
      // so use the native "Choose Application" dialog instead.
      applicationPath = await MacOsAppPicker.PickApplicationAsync();
    }
    else
    {
      IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(
        new FilePickerOpenOptions
        {
          Title = "Select Application",
          AllowMultiple = false,
        });
      applicationPath = files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    if (!string.IsNullOrEmpty(applicationPath))
    {
      _viewModel.AddApplication(applicationPath);
    }
  }

  private void SaveButton_OnClick(object? sender, RoutedEventArgs e)
  {
    Close(_viewModel.ToOpenWithApps());
  }

  private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
  {
    Close(null);
  }
}
