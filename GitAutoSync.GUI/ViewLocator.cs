using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;

namespace GitAutoSync.GUI;

public class ViewLocator : IDataTemplate
{
  public Control? Build(object? data)
  {
    if (data is null)
    {
      return null;
    }

    string name = data.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
#pragma warning disable IL2057 // Type.GetType - required for Avalonia ViewLocator convention
    Type? type = Type.GetType(name);
#pragma warning restore IL2057

    if (type != null)
    {
      Control control = (Control) Activator.CreateInstance(type)!;
      control.DataContext = data;
      return control;
    }

    return new TextBlock {Text = "Not Found: " + name};
  }

  public bool Match(object? data)
  {
    return data is ViewModelBase;
  }
}