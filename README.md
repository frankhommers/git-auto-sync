# Git Auto Sync

A cross-platform Git repository synchronization tool that automatically commits and pushes changes to keep your repositories in sync. Built with .NET 10 and featuring both a modern GUI and background daemon for seamless operation.

## Features

- **Automatic Synchronization**: Monitors repositories for changes and automatically commits/pushes
- **Multi-Repository Support**: Manage multiple Git repositories simultaneously
- **Cross-Platform**: Works on macOS, Linux, and Windows
- **Modern Architecture**: Client-server design with daemon and GUI separation
- **Real-Time Updates**: WebSocket-based live updates in the GUI
- **System Integration**: Start automatically on system login (macOS Launch Agent, Linux systemd, Windows Task Scheduler)
- **Hostname Filtering**: Configure repositories to sync only on specific machines
- **System Tray**: Minimize to system tray for unobtrusive background operation

## Architecture

Git Auto Sync consists of three main components:

1. **GitAutoSync.Daemon** - Background service that monitors repositories and performs Git operations
2. **GitAutoSync.GUI** - Avalonia-based desktop application for managing the daemon
3. **GitAutoSync.Core** - Shared core functionality and domain models

### Communication Flow

```
┌─────────────┐         HTTP/WebSocket         ┌──────────────┐
│             │ ────────────────────────────>  │              │
│  GUI Client │                                │    Daemon    │
│  (Avalonia) │ <────────────────────────────  │  (ASP.NET)   │
└─────────────┘    Real-time status updates    └──────────────┘
                                                       │
                                                       │ Monitors
                                                       ▼
                                               ┌──────────────┐
                                               │ Git Repos    │
                                               │ on Disk      │
                                               └──────────────┘
```

## Requirements

- **.NET 10 SDK** or later
- **Git** installed and available in PATH
- Supported Operating Systems:
  - macOS 10.15 or later
  - Linux (systemd-based distributions)
  - Windows 10 or later

## Getting Started

### Building from Source

```bash
# Clone the repository
git clone https://github.com/yourusername/git-auto-sync.git
cd git-auto-sync

# Restore dependencies
dotnet restore

# Build all projects
dotnet build
```

### Running the Daemon

The daemon must be running before using the GUI:

```bash
dotnet run --project GitAutoSync.Daemon
```

The daemon will start on `http://127.0.0.1:52847` by default.

### Running the Console app (with terminal notifications)

```bash
dotnet run --project GitAutoSync.Console -- \
  --notification-mode auto-terminal-first \
  --terminal-notification-preference osc9-only
```

Valid notification modes (console default is `auto-terminal-first`):

- `auto-desktop-first`
- `auto-terminal-first`
- `desktop-only`
- `terminal-only`
- `off`

### Running the GUI

```bash
dotnet run --project GitAutoSync.GUI
```

For minimized startup (system tray):

```bash
dotnet run --project GitAutoSync.GUI -- --minimized
```

For auto-start with configuration:

```bash
dotnet run --project GitAutoSync.GUI -- --config-file=/path/to/config.toml --auto-start
```

## Configuration

Git Auto Sync uses TOML configuration files to define which repositories to monitor.

### Configuration File Format

```toml
[[repo]]
name = "MyProject"
path = "/Users/username/projects/myproject"
hosts = ["MacBook-Pro", "work-laptop"]  # Optional: only sync on these machines

[[repo]]
name = "Documentation"
path = "/Users/username/documents/docs"
# No hosts = syncs on all machines
```

### Configuration Options

- **name**: Display name for the repository
- **path**: Absolute path to the Git repository
- **hosts**: (Optional) List of hostnames where this repository should sync

### Default Configuration Location

- **macOS/Linux**: `~/.config/git-auto-sync/config.toml`
- **Windows**: `%APPDATA%\git-auto-sync\config.toml`

## API Documentation

The daemon exposes a REST API and WebSocket interface for management.

### REST API Endpoints

#### Health Check

```
GET /health
```

Returns daemon health status.

#### Repository Management

```
GET /api/repositories
```
List all repositories.

```
POST /api/repositories
Content-Type: application/json

{
  "name": "MyRepo",
  "path": "/path/to/repo"
}
```
Add a new repository.

```
DELETE /api/repositories/{id}
```
Remove a repository.

```
POST /api/repositories/{id}/start
```
Start monitoring a repository.

```
POST /api/repositories/{id}/stop
```
Stop monitoring a repository.

```
POST /api/repositories/start-all
```
Start monitoring all repositories.

```
POST /api/repositories/stop-all
```
Stop monitoring all repositories.

#### Configuration

```
POST /api/config/load
Content-Type: application/json

{
  "path": "/path/to/config.toml"
}
```
Load configuration from file.

```
POST /api/config/save
Content-Type: application/json

{
  "path": "/path/to/config.toml"
}
```
Save current configuration to file.

#### Startup Management

```
GET /api/startup/status
```
Check if startup on login is enabled.

```
POST /api/startup/enable
Content-Type: application/json

{
  "configPath": "/path/to/config.toml"
}
```
Enable startup on login.

```
POST /api/startup/disable
```
Disable startup on login.

### WebSocket Events

Connect to `ws://127.0.0.1:52847/ws` to receive real-time events.

#### Event Types

**repository_started**
```json
{
  "type": "repository_started",
  "timestamp": "2026-02-15T10:30:00Z",
  "data": {
    "id": "guid",
    "name": "MyRepo"
  }
}
```

**repository_stopped**
```json
{
  "type": "repository_stopped",
  "timestamp": "2026-02-15T10:30:00Z",
  "data": {
    "id": "guid",
    "name": "MyRepo"
  }
}
```

**repository_activity**
```json
{
  "type": "repository_activity",
  "timestamp": "2026-02-15T10:30:00Z",
  "data": {
    "id": "guid",
    "name": "MyRepo",
    "message": "Changes detected and committed"
  }
}
```

**repository_added**
```json
{
  "type": "repository_added",
  "timestamp": "2026-02-15T10:30:00Z",
  "data": {
    "id": "guid",
    "name": "MyRepo",
    "path": "/path/to/repo"
  }
}
```

**repository_removed**
```json
{
  "type": "repository_removed",
  "timestamp": "2026-02-15T10:30:00Z",
  "data": {
    "id": "guid"
  }
}
```

## Development Guide

### Project Structure

```
git-auto-sync/
├── GitAutoSync.Core/           # Shared core functionality
│   ├── Config/                 # Configuration models
│   ├── GitAutoSyncDirectoryWorker.cs  # Repository monitoring
│   └── StartupManager.cs       # System startup integration
├── GitAutoSync.Daemon/         # Background daemon service
│   ├── Api/                    # REST and WebSocket endpoints
│   ├── Models/                 # Data transfer objects
│   └── Services/               # Business logic services
├── GitAutoSync.GUI/            # Avalonia desktop application
│   ├── Assets/                 # Icons and resources
│   ├── Commands/               # UI commands
│   ├── Models/                 # View models
│   ├── Services/               # Daemon client and WebSocket
│   ├── ViewModels/             # MVVM view models
│   └── Views/                  # XAML views
└── GitAutoSync.Console/        # Legacy console application
```

### Key Technologies

- **.NET 10** - Runtime and framework
- **ASP.NET Core** - Web API and WebSocket hosting
- **Avalonia 11.2.2** - Cross-platform UI framework
- **ReactiveUI** - MVVM framework
- **Serilog** - Structured logging
- **Tomlet** - TOML configuration parsing
- **CliWrap** - Git command execution
- **Notifs** - Cross-platform desktop notifications (NuGet.org)

### Building for Production

```bash
# Build daemon
dotnet publish GitAutoSync.Daemon -c Release -o ./publish/daemon

# Build GUI
dotnet publish GitAutoSync.GUI -c Release -o ./publish/gui
```

### Build a macOS App Bundle

Use the helper script to create a runnable `.app` bundle (GUI + daemon packaged together):

```bash
./scripts/build-macos-app.sh
open "dist/Git Auto Sync.app"
```

Optional flags:

- `--rid osx-arm64|osx-x64`
- `--configuration Release|Debug`
- `--output <folder>`
- `--codesign` (ad-hoc codesign)

### Automatic DMG assets for GitHub Releases

This repository includes a GitHub Actions workflow at `.github/workflows/release-dmg.yml`.

- On `Release -> published`, it automatically builds macOS app bundles for `osx-arm64` and `osx-x64`.
- It then packages both as DMG files and uploads them to the matching GitHub release.

## Versioning

This project follows semantic versioning (`MAJOR.MINOR.PATCH`).

- The baseline version is defined in `Directory.Build.props` via `VersionPrefix`.
- For release builds, use git tags like `v1.2.3`.
- The release workflow strips the `v` and embeds `1.2.3` into binaries.
- DMG artifacts are published as:
  - `GitAutoSync-v1.2.3-osx-arm64.dmg`
  - `GitAutoSync-v1.2.3-osx-x64.dmg`
  - plus matching `.sha256` checksum files.

Typical release flow:

1. Bump `VersionPrefix` in `Directory.Build.props`.
2. Commit and tag: `git tag vX.Y.Z`.
3. Push commit + tag.
4. Publish a GitHub Release for that tag.

### Running Tests

```bash
dotnet test
```

### Adding New Features

1. **Core Functionality**: Add to `GitAutoSync.Core`
2. **Daemon API**: Add endpoints to `GitAutoSync.Daemon/Api`
3. **GUI Integration**: Update `DaemonClient` and view models in `GitAutoSync.GUI`

### Logging

The application uses Serilog for structured logging:

- **Daemon logs**: `logs/daemon-YYYYMMDD.log`
- **GUI logs**: Displayed in application log panel
- **Console output**: Enabled for both daemon and GUI

## Startup Configuration

### macOS (Launch Agent)

Enable startup on login:

```bash
# Via GUI: Click "Add to Startup on Login"
# Or manually with daemon API:
curl -X POST http://127.0.0.1:52847/api/startup/enable \
  -H "Content-Type: application/json" \
  -d '{"configPath":"/path/to/config.toml"}'
```

The daemon creates a launch agent at:
`~/Library/LaunchAgents/com.gitautosync.daemon.plist`

For the packaged GUI app, you can also manage auto-login with:

```bash
./scripts/setup-autologin.sh enable --config /absolute/path/to/config.toml
# later
./scripts/setup-autologin.sh disable
```

### Linux (systemd)

Create a systemd user service at `~/.config/systemd/user/git-auto-sync.service`:

```ini
[Unit]
Description=Git Auto Sync Daemon
After=network.target

[Service]
Type=simple
ExecStart=/path/to/GitAutoSync.Daemon
Restart=on-failure

[Install]
WantedBy=default.target
```

Enable the service:

```bash
systemctl --user enable git-auto-sync.service
systemctl --user start git-auto-sync.service
```

### Windows (Task Scheduler)

Enable startup on login via the GUI or use Task Scheduler to create a task that runs on login.

## Troubleshooting

### Daemon Won't Start

- Ensure port 52847 is not in use: `lsof -i :52847` (macOS/Linux) or `netstat -ano | findstr :52847` (Windows)
- Check logs in `logs/daemon-*.log`

### GUI Can't Connect to Daemon

- Verify daemon is running: `curl http://127.0.0.1:52847/health`
- Check firewall settings
- Ensure daemon is listening on correct port

### Repositories Not Syncing

- Verify Git is installed: `git --version`
- Check repository paths in configuration
- Review daemon logs for Git errors
- Ensure repositories have remote configured: `git remote -v`

### Permission Issues

- Ensure Git repositories are readable/writable
- Check SSH keys for Git authentication
- Verify file system permissions

## Contributing

Contributions are welcome.

Please read `CONTRIBUTING.md` and `CODE_OF_CONDUCT.md` before opening a pull request.

## License

This project is licensed under the MIT License. See `LICENSE`.

Third-party attributions are listed in `THIRD_PARTY_NOTICES.md`.

## Credits

Built with:
- [.NET](https://dotnet.microsoft.com/)
- [Avalonia](https://avaloniaui.net/)
- [ASP.NET Core](https://dotnet.microsoft.com/apps/aspnet)
- [Serilog](https://serilog.net/)
- [ReactiveUI](https://www.reactiveui.net/)

## Support

For issues and questions:
- Open an issue on GitHub
- Check existing documentation
- Review logs for error messages

## Security

For responsible vulnerability reporting, see `SECURITY.md`.
