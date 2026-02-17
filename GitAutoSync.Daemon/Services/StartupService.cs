using GitAutoSync.Core;

namespace GitAutoSync.Daemon.Services;

public class StartupService
{
  private readonly IStartupManager _startupManager;
  private readonly ILogger<StartupService> _logger;

  public StartupService(IStartupManager startupManager, ILogger<StartupService> logger)
  {
    _startupManager = startupManager;
    _logger = logger;
  }

  public async Task<bool> IsEnabledAsync()
  {
    try
    {
      return await _startupManager.IsEnabledAsync();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to check startup status");
      return false;
    }
  }

  public async Task<bool> EnableAsync(string configFilePath)
  {
    try
    {
      _logger.LogInformation("Enabling startup on login with config: {ConfigPath}", configFilePath);
      bool result = await _startupManager.EnableAsync(configFilePath);

      if (result)
      {
        _logger.LogInformation("Successfully enabled startup on login");
      }
      else
      {
        _logger.LogWarning("Failed to enable startup on login");
      }

      return result;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error enabling startup on login");
      return false;
    }
  }

  public async Task<bool> DisableAsync()
  {
    try
    {
      _logger.LogInformation("Disabling startup on login");
      bool result = await _startupManager.DisableAsync();

      if (result)
      {
        _logger.LogInformation("Successfully disabled startup on login");
      }
      else
      {
        _logger.LogWarning("Failed to disable startup on login");
      }

      return result;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error disabling startup on login");
      return false;
    }
  }

  public bool IsSupported()
  {
    return _startupManager.IsSupported;
  }

  public string GetStatusMessage()
  {
    return _startupManager.GetStatusMessage();
  }
}