using System.Windows.Input;
using Avalonia.Threading;

namespace GitAutoSync.GUI.Commands;

public class ThreadSafeCommand : ICommand
{
  private readonly Func<Task> _execute;
  private readonly Func<bool>? _canExecute;
  private bool _isExecuting;

  public ThreadSafeCommand(Func<Task> execute, Func<bool>? canExecute = null)
  {
    _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    _canExecute = canExecute;
  }

  public ThreadSafeCommand(Action execute, Func<bool>? canExecute = null)
  {
    _execute = () =>
    {
      execute();
      return Task.CompletedTask;
    };
    _canExecute = canExecute;
  }

  public event EventHandler? CanExecuteChanged;

  public bool CanExecute(object? parameter)
  {
    if (Dispatcher.UIThread.CheckAccess())
    {
      return !_isExecuting && (_canExecute?.Invoke() ?? true);
    }
    else
    {
      return Dispatcher.UIThread.Invoke(() => !_isExecuting && (_canExecute?.Invoke() ?? true));
    }
  }

  public async void Execute(object? parameter)
  {
    if (!CanExecute(parameter))
    {
      return;
    }

    try
    {
      _isExecuting = true;
      RaiseCanExecuteChanged();

      await _execute();
    }
    finally
    {
      _isExecuting = false;
      RaiseCanExecuteChanged();
    }
  }

  public void RaiseCanExecuteChanged()
  {
    if (Dispatcher.UIThread.CheckAccess())
    {
      CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
    else
    {
      Dispatcher.UIThread.Post(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
    }
  }
}

public class ThreadSafeCommand<T> : ICommand
{
  private readonly Func<T?, Task> _execute;
  private readonly Func<T?, bool>? _canExecute;
  private bool _isExecuting;

  public ThreadSafeCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
  {
    _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    _canExecute = canExecute;
  }

  public ThreadSafeCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
  {
    _execute = (param) =>
    {
      execute(param);
      return Task.CompletedTask;
    };
    _canExecute = canExecute;
  }

  public event EventHandler? CanExecuteChanged;

  public bool CanExecute(object? parameter)
  {
    if (Dispatcher.UIThread.CheckAccess())
    {
      return !_isExecuting && (_canExecute?.Invoke((T?) parameter) ?? true);
    }
    else
    {
      return Dispatcher.UIThread.Invoke(() => !_isExecuting && (_canExecute?.Invoke((T?) parameter) ?? true));
    }
  }

  public async void Execute(object? parameter)
  {
    if (!CanExecute(parameter))
    {
      return;
    }

    try
    {
      _isExecuting = true;
      RaiseCanExecuteChanged();

      await _execute((T?) parameter);
    }
    finally
    {
      _isExecuting = false;
      RaiseCanExecuteChanged();
    }
  }

  public void RaiseCanExecuteChanged()
  {
    if (Dispatcher.UIThread.CheckAccess())
    {
      CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
    else
    {
      Dispatcher.UIThread.Post(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
    }
  }
}