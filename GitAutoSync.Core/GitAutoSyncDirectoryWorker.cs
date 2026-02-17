using System.Collections.Concurrent;
using System.Text;
using System.Timers;
using CliWrap;
using Microsoft.Extensions.Logging;

namespace GitAutoSync.Core;

public class GitAutoSyncDirectoryWorker : IDisposable
{
  private readonly ILogger<GitAutoSyncDirectoryWorker> _logger;
  private readonly string _repoName;
  private readonly DirectoryInfo _repoDir;
  private readonly DirectoryInfo _repoGitDir;
  private readonly System.Timers.Timer _afterFileSystemWatcherTimer;
  private readonly System.Timers.Timer _periodicTimer;
  private readonly ConcurrentQueue<(string? filePath, EventType eventType)> _queue = new();
  private readonly SemaphoreSlim _queueSemaphore = new(1, 1);
  private readonly SemaphoreSlim _gitSemaphore = new(1, 1);
  private readonly FileSystemWatcher _watcher;
  private bool _disposed = false;

  public enum EventType
  {
    Created,
    Changed,
    Deleted,
    Renamed,
    ForcedCheck,
  }

  public GitAutoSyncDirectoryWorker(ILogger<GitAutoSyncDirectoryWorker> logger, string repoName, string repoPath)
  {
    _logger = logger;
    _repoName = repoName;
    logger.LogInformation("Initializing RepoFileSystemWatcher");
    _repoDir = new DirectoryInfo(FileUtils.GetExactPathName(repoPath));
    _repoGitDir = new DirectoryInfo(FileUtils.GetExactPathName(Path.Combine(_repoDir.FullName, ".git")));
    _afterFileSystemWatcherTimer = new System.Timers.Timer(new TimeSpan(0, 0, 5));
    _afterFileSystemWatcherTimer.Elapsed += AfterFileSystemWatcherTimerOnElapsed;
    _afterFileSystemWatcherTimer.AutoReset = false;
    if (!_repoGitDir.Exists)
    {
      throw new ArgumentException("Does not contain a .git directory.");
    }

    _watcher = new FileSystemWatcher(repoPath);
    _watcher.NotifyFilter = AllNotifyFilters();
    _watcher.Changed += WatcherOnChanged;
    _watcher.Created += WatcherOnCreated;
    _watcher.Deleted += WatcherOnDeleted;
    _watcher.Renamed += WatcherOnRenamed;
    _watcher.Error += WatcherOnError;
    _watcher.IncludeSubdirectories = true;
    _watcher.EnableRaisingEvents = true;
    Enqueue(null, EventType.ForcedCheck);

    _periodicTimer = new System.Timers.Timer(new TimeSpan(0, 5, 0));
    _periodicTimer.Elapsed += PeriodicTimerOnElapsed;
    _periodicTimer.AutoReset = true;
    _periodicTimer.Start();
    StartOrRestartAfterFileSystemWatcherTimer();
  }

  private void PeriodicTimerOnElapsed(object? sender, ElapsedEventArgs e)
  {
    _logger.LogInformation("Periodic timer enqueueing forced check event");
    Enqueue(null, EventType.ForcedCheck);
    SynchronizeIfNeeded();
  }

  private NotifyFilters AllNotifyFilters()
  {
    FileSystemWatcher watcher = new();
    watcher.NotifyFilter = 0;
    for (int i = 0; i < 32; i++)
    {
      try
      {
        watcher.NotifyFilter |= (NotifyFilters) (1 << i);
      }
      catch
      {
        //ignored
      }
    }

    return watcher.NotifyFilter;
  }

  private void StartOrRestartAfterFileSystemWatcherTimer()
  {
    _afterFileSystemWatcherTimer.Stop();
    _afterFileSystemWatcherTimer.AutoReset = false;
    _afterFileSystemWatcherTimer.Start();
  }

  private void AfterFileSystemWatcherTimerOnElapsed(object? sender, ElapsedEventArgs e)
  {
    SynchronizeIfNeeded();
  }

  private void SynchronizeIfNeeded()
  {
    _queueSemaphore.Wait();
    List<(string? filePath, EventType eventType)> list = _queue.ToList();
    _queue.Clear();
    _queueSemaphore.Release();
    if (!list.Any())
    {
      return;
    }

    _ = Task.Run(async () => await SynchronizeAsync(list));
  }

  private async Task<(CommandResult commandResult, string stdOut, string stdErr)> ExecuteGitAsync(
    string arguments,
    bool notifyOnError = true)
  {
    _logger.LogInformation($"Executing: git {arguments}");
    StringBuilder sbStdOut = new();
    StringBuilder sbStdErr = new();
    CommandResult statusResult = await Cli
      .Wrap("git")
      .WithValidation(CommandResultValidation.None)
      .WithWorkingDirectory(_repoDir.FullName)
      .WithArguments(arguments)
      .WithStandardOutputPipe(PipeTarget.ToStringBuilder(sbStdOut))
      .WithStandardErrorPipe(PipeTarget.ToStringBuilder(sbStdErr))
      .ExecuteAsync().ConfigureAwait(false);
    _logger.LogInformation($"Status: {statusResult.ExitCode}");
    string stdOut = sbStdOut.ToString();
    string stdErr = sbStdErr.ToString();
    if (!string.IsNullOrWhiteSpace(stdOut))
    {
      _logger.LogDebug($"Output: {stdOut}");
    }

    if (!string.IsNullOrWhiteSpace(stdErr))
    {
      if (statusResult.ExitCode != 0)
      {
        _logger.LogError($"Error: {stdErr}");
      }
      else
      {
        _logger.LogDebug($"Stderr: {stdErr}");
      }
    }

    if (statusResult.ExitCode != 0 && notifyOnError)
    {
      _logger.LogError($"Git error occurred, error executing git {arguments}");
      Notifs.Notifs.NotifyAsync("GitAutoSync", _repoName, $"Error executing git {arguments}").ConfigureAwait(false)
        .GetAwaiter().GetResult();
    }

    return (statusResult, stdOut, stdErr);
  }

  private async Task SynchronizeAsync(List<(string? filePath, EventType eventType)> list)
  {
    _logger.LogInformation("Synchronizing");
    await _gitSemaphore.WaitAsync();
    try
    {
      bool mustProcess = false;
      if (list.Any(s => s.eventType == EventType.ForcedCheck))
      {
        mustProcess = true;
      }
      else
      {
        List<(FileSystemInfo, EventType)> fileSystemInfos =
          list.Where(x => !string.IsNullOrWhiteSpace(x.filePath))
            .Select(x => (FileUtils.GetFileOrDirectory(x.filePath!), x.eventType))
            .Where(f => !FileUtils.IsDescendantOfDirectory(_repoGitDir, f.Item1)).ToList();

        if (fileSystemInfos.Any())
        {
          mustProcess = true;
        }
      }

      if (!mustProcess)
      {
        return;
      }


      _logger.LogInformation("Checking local status");
      (_, string gitStatusOutput, _) = await ExecuteGitAsync("status --porcelain");
      // Parse the output of the `status` command to get the list of changed files
      List<(string status, string filePath)> changedOutgoingFiles = ParseStatusOutput(gitStatusOutput);
      _logger.LogInformation("Local: {FileCount} files changed", changedOutgoingFiles.Count);

      _logger.LogInformation("Fetching remote status");
      await ExecuteGitAsync("fetch --prune --all --force --verbose");

      (bool hasUpstream, int aheadCount, int behindCount) = await GetAheadBehindAsync();
      if (hasUpstream)
      {
        _logger.LogInformation("Remote divergence: ahead={Ahead}, behind={Behind}", aheadCount, behindCount);
      }
      else
      {
        _logger.LogInformation("No upstream tracking branch configured; pull/push sync skipped");
      }

      string commitMessage = string.Empty;
      bool createdCommit = false;
      if (changedOutgoingFiles.Any())
      {
        await ExecuteGitAsync("add --all");

        (CommandResult _, gitStatusOutput, _) = await ExecuteGitAsync("status --porcelain");
        // Parse the output of the `status` command to get the list of changed files
        changedOutgoingFiles = ParseStatusOutput(gitStatusOutput);

        // Build the commit message
        commitMessage = BuildCommitMessage(changedOutgoingFiles);
        (CommandResult commitResult, _, _) = await ExecuteGitAsync($"commit -m \"{commitMessage}\"");
        createdCommit = commitResult.ExitCode == 0;
      }

      if (hasUpstream && behindCount > 0)
      {
        (CommandResult commandResult, string stdOut, string stdErr) pullResult =
          await ExecuteGitAsync("pull --rebase --autostash --stat");
        if (pullResult.commandResult.ExitCode != 0)
        {
          _logger.LogWarning("Pull with rebase failed");
          Notifs.Notifs.NotifyAsync(
              "GitAutoSync",
              _repoName,
              $"Pull/rebase failed (stdout: {pullResult.stdOut}, stderr: {pullResult.stdErr})").ConfigureAwait(false)
            .GetAwaiter().GetResult();
          return;
        }
        else if (!string.IsNullOrWhiteSpace(pullResult.stdOut))
        {
          Notifs.Notifs
            .NotifyAsync("GitAutoSync", "Synchronized (rebase) files from remote to local", pullResult.stdOut)
            .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        (hasUpstream, aheadCount, behindCount) = await GetAheadBehindAsync();
      }

      bool shouldPush = createdCommit || (hasUpstream && aheadCount > 0);
      if (hasUpstream && shouldPush)
      {
        (CommandResult commandResult, string stdOut, string stdErr)
          pushResult = await ExecuteGitAsync("push --verbose");
        if (pushResult.commandResult.ExitCode != 0)
        {
          _logger.LogWarning("Push failed");
          Notifs.Notifs.NotifyAsync(
              "GitAutoSync",
              _repoName,
              $"Push failed (stdout: {pushResult.stdOut}, stderr: {pushResult.stdErr})").ConfigureAwait(false)
            .GetAwaiter().GetResult();
        }
        else
        {
          Notifs.Notifs.NotifyAsync(
              "GitAutoSync",
              _repoName,
              $"Synchronized (push) files from local to remote {commitMessage}").ConfigureAwait(false).GetAwaiter()
            .GetResult();
        }
      }
    }
    finally
    {
      _gitSemaphore.Release();
    }
  }

  private async Task<(bool hasUpstream, int aheadCount, int behindCount)> GetAheadBehindAsync()
  {
    (CommandResult commandResult, string stdOut, string stdErr) upstreamResult =
      await ExecuteGitAsync("rev-parse --abbrev-ref --symbolic-full-name @{u}", false);

    if (upstreamResult.commandResult.ExitCode != 0)
    {
      return (false, 0, 0);
    }

    (CommandResult commandResult, string stdOut, string stdErr) divergenceResult =
      await ExecuteGitAsync("rev-list --left-right --count HEAD...@{u}", false);

    if (divergenceResult.commandResult.ExitCode != 0)
    {
      return (false, 0, 0);
    }

    if (!TryParseAheadBehind(divergenceResult.stdOut, out int aheadCount, out int behindCount))
    {
      _logger.LogWarning("Could not parse rev-list output: '{Output}'", divergenceResult.stdOut.Trim());
      return (false, 0, 0);
    }

    return (true, aheadCount, behindCount);
  }

  private static bool TryParseAheadBehind(string output, out int aheadCount, out int behindCount)
  {
    aheadCount = 0;
    behindCount = 0;

    if (string.IsNullOrWhiteSpace(output))
    {
      return false;
    }

    string[] parts = output.Trim().Split(new[] {' ', '\t'}, StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length < 2)
    {
      return false;
    }

    return int.TryParse(parts[0], out aheadCount) && int.TryParse(parts[1], out behindCount);
  }

  private List<(string status, string filePath)> ParseStatusOutput(string statusOutput)
  {
    if (string.IsNullOrWhiteSpace(statusOutput))
    {
      return new List<(string status, string filePath)>();
    }

    string[] lines = statusOutput.Split(new[] {"\n", "\r\n"}, StringSplitOptions.RemoveEmptyEntries);
    List<(string status, string filePath)> changedFiles = new();

    foreach (string line in lines)
    {
      string status = line.Substring(0, 2);
      if (status == "!!")
      {
        continue; // Skip untracked files
      }

      string filePath = line.Substring(3);
      changedFiles.Add((status, filePath));
    }

    return changedFiles;
  }

  private string BuildCommitMessage(List<(string status, string filePath)> changedFiles)
  {
    StringBuilder sb = new();
    if (!changedFiles.Any())
    {
      return "Auto commit";
    }

    bool first = true;
    foreach ((string status, string filePath) in changedFiles)
    {
      if (first)
      {
        first = false;
      }
      else
      {
        sb.Append(", ");
      }

      sb.Append($"{status.ToUpper().Trim()} {filePath.Trim().Replace("\"", "\\\"")}");
    }

    return sb.ToString();
  }


  private void Enqueue(string? filePath, EventType eventType)
  {
    _queueSemaphore.Wait();
    (string? filePath, EventType eventType) item = (filePath, eventType);
    if (!_queue.Contains(item))
    {
      _queue.Enqueue(item);
    }

    _queueSemaphore.Release();
  }

  private void WatcherOnError(object sender, ErrorEventArgs e)
  {
    _logger.LogDebug("File watcher error: {Exception}", e.GetException());
  }

  private void WatcherOnRenamed(object sender, RenamedEventArgs e)
  {
    if (IsGitFile(e.FullPath))
    {
      return;
    }

    _logger.LogDebug("File rename detected: {FullPath}", e.FullPath);
    Enqueue(e.FullPath, EventType.Renamed);
    StartOrRestartAfterFileSystemWatcherTimer();
  }


  private void WatcherOnDeleted(object sender, FileSystemEventArgs e)
  {
    if (IsGitFile(e.FullPath))
    {
      return;
    }

    _logger.LogDebug("File delete detected: {FullPath}", e.FullPath);
    Enqueue(e.FullPath, EventType.Deleted);
    StartOrRestartAfterFileSystemWatcherTimer();
  }

  private void WatcherOnCreated(object sender, FileSystemEventArgs e)
  {
    if (IsGitFile(e.FullPath))
    {
      return;
    }

    _logger.LogDebug("File create detected: {FullPath}", e.FullPath);
    Enqueue(e.FullPath, EventType.Created);
    StartOrRestartAfterFileSystemWatcherTimer();
  }

  private void WatcherOnChanged(object sender, FileSystemEventArgs e)
  {
    if (IsGitFile(e.FullPath))
    {
      return;
    }

    _logger.LogDebug("File change detected: {FullPath}", e.FullPath);
    Enqueue(e.FullPath, EventType.Changed);

    StartOrRestartAfterFileSystemWatcherTimer();
  }

  private bool IsGitFile(string path)
  {
    return FileUtils.IsDescendantOfDirectory(_repoGitDir, FileUtils.GetFileOrDirectory(path));
  }

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing)
  {
    if (!_disposed && disposing)
    {
      _watcher?.Dispose();
      _afterFileSystemWatcherTimer?.Dispose();
      _periodicTimer?.Dispose();
      _queueSemaphore?.Dispose();
      _gitSemaphore?.Dispose();
      _disposed = true;
    }
  }
}