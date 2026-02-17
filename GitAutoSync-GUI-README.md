# Git Auto Sync - GUI Documentation

## Overview

Git Auto Sync now includes a modern GUI alongside the original console interface. The GUI is built with Avalonia, providing a cross-platform desktop application experience.

## Running the Application

### Console Mode (Original)
```bash
dotnet run --project GitAutoSync
```

### GUI Mode (New)
```bash
dotnet run --project GitAutoSync -- --gui
```
or
```bash
dotnet run --project GitAutoSync -- -g
```

## GUI Features

### Main Window Components

1. **Configuration Section**
   - Browse and select TOML configuration files
   - Load repository configurations from file

2. **Repository Management**
   - Add repositories by browsing for git directories
   - Start/stop individual repositories or all at once
   - View repository status (Running, Stopped, Error)
   - Remove repositories from monitoring list

3. **Activity Log**
   - Real-time log entries with timestamps
   - Color-coded log levels (INFO, ERROR, WARNING, DEBUG)
   - Repository-specific logging
   - Clear log functionality

4. **Status Bar**
   - Current operation status
   - Count of active vs total repositories

### Repository Status Indicators

- **Green**: Repository is actively being monitored
- **Red**: Error occurred with repository
- **Gray**: Repository is stopped

### Configuration

The GUI can load the same TOML configuration files used by the console version:

```toml
[[repo]]
name = "MyProject"
path = "/path/to/my/project"
hosts = ["hostname1", "hostname2"]  # Optional: only run on specific hosts
```

## Architecture

### Key Components

- **MainWindow**: Primary application window with repository management
- **MainWindowViewModel**: MVVM pattern view model handling business logic
- **RepositoryViewModel**: Individual repository representation
- **GitAutoSyncDirectoryWorker**: Core file system monitoring (shared with console)
- **SerilogLoggerAdapter**: Bridges Serilog with Microsoft.Extensions.Logging
- **GuiLogSink**: Custom Serilog sink for GUI log display

### Shared Code

The GUI reuses the existing core components:
- `GitAutoSyncDirectoryWorker` for file system monitoring
- `Config` classes for TOML configuration
- Git synchronization logic

## Development

### Dependencies

- **Avalonia 11.0.10**: Cross-platform UI framework
- **Avalonia.ReactiveUI**: MVVM support
- **ReactiveUI**: Reactive programming patterns
- **Serilog**: Logging framework
- **Existing dependencies**: CliWrap, Cocona, Tomlet, etc.

### Building

```bash
dotnet restore
dotnet build
```

### Project Structure

```
GitAutoSync/
├── GUI/
│   ├── App.axaml              # Application definition
│   ├── App.axaml.cs
│   ├── ViewLocator.cs         # View resolution
│   ├── ViewModelBase.cs       # Base class for view models
│   ├── SerilogLoggerAdapter.cs # Logger integration
│   ├── GuiLogSink.cs          # GUI log sink
│   ├── Views/
│   │   ├── MainWindow.axaml   # Main window XAML
│   │   └── MainWindow.axaml.cs
│   └── ViewModels/
│       ├── MainWindowViewModel.cs
│       ├── RepositoryViewModel.cs
│       └── LogEntryViewModel.cs
├── Config/                    # Shared configuration
├── Program.cs                 # Updated to support both modes
└── GitAutoSyncDirectoryWorker.cs # Core monitoring logic
```

## Command Line Options

The application now supports both modes through command line arguments:

- `--gui` or `-g`: Launch GUI mode
- `--config-file`: Specify configuration file (both modes)
- `--add-to-login`: Add to startup (console mode)
- `--help`: Show help information

## Future Enhancements

Potential improvements for the GUI:
- System tray integration
- Settings dialog for configuration editing
- Git commit history viewer
- Performance metrics and statistics
- Dark/light theme toggle
- Auto-start with system option in GUI
