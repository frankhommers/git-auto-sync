using GitAutoSync.Daemon.Models;
using System.Collections.Concurrent;

namespace GitAutoSync.Daemon.Services;

public class EventBus
{
  private readonly ConcurrentBag<Func<DaemonEvent, Task>> _subscribers = new();

  public void Subscribe(Func<DaemonEvent, Task> handler)
  {
    _subscribers.Add(handler);
  }

  public async Task PublishAsync(DaemonEvent ev)
  {
    IEnumerable<Task> tasks = _subscribers.Select(handler => handler(ev));
    await Task.WhenAll(tasks);
  }

  public void PublishLog(string repository, string level, string message)
  {
    DaemonEvent ev = new(
      "log",
      DateTime.UtcNow,
      new LogEventData(repository, level, message)
    );

    _ = PublishAsync(ev); // Fire and forget
  }

  public void PublishStatusChange(string repositoryId, bool isRunning, string status)
  {
    DaemonEvent ev = new(
      "status",
      DateTime.UtcNow,
      new StatusEventData(repositoryId, isRunning, status)
    );

    _ = PublishAsync(ev);
  }
}