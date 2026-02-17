namespace GitAutoSync.Daemon.Models;

public record DaemonEvent(
  string Type,
  DateTime Timestamp,
  object Data
);

public record LogEventData(
  string Repository,
  string Level,
  string Message
);

public record StatusEventData(
  string RepositoryId,
  bool IsRunning,
  string Status
);