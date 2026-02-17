using GitAutoSync.GUI.ViewModels;
using Serilog.Core;
using Serilog.Events;

namespace GitAutoSync.GUI;

public class GuiLogSink : ILogEventSink
{
  private readonly MainWindowViewModel _viewModel;

  public GuiLogSink(MainWindowViewModel viewModel)
  {
    _viewModel = viewModel;
  }

  public void Emit(LogEvent logEvent)
  {
    string level = logEvent.Level.ToString().ToUpper();
    string repository = logEvent.Properties.TryGetValue("RepositoryName", out LogEventPropertyValue? repoProperty)
      ? repoProperty.ToString().Trim('"')
      : "System";
    string message = logEvent.RenderMessage();

    // AddLogEntry now handles UI thread marshalling internally
    _viewModel.AddLogEntry(level, repository, message);
  }
}