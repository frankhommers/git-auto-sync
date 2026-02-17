# Git Auto Sync - Daemon + UI Architecture Design

**Date:** 2026-02-15
**Author:** Claude Code
**Status:** Approved

## Overview

Upgrade Git Auto Sync from .NET 9 to .NET 10 and refactor into a robust daemon + UI architecture. The daemon runs independently as a background service, while the UI acts as a thin client with system tray integration. This ensures git repository monitoring continues even if the UI crashes or closes.

## Goals

1. **Upgrade to .NET 10** - Update all projects to target .NET 10
2. **Update NuGet packages** - Refresh all dependencies to latest compatible versions
3. **Daemon Architecture** - Create standalone daemon with HTTP/WebSocket API
4. **System Tray UI** - GUI becomes thin client with always-visible tray icon
5. **Hybrid Deployment** - UI auto-starts daemon if not running; daemon can also run standalone
6. **Auto-start on Login** - Seamless startup with minimized/tray behavior
7. **Real-time Updates** - WebSocket-based live status and log streaming

## Architecture

### Component Overview

```
┌─────────────────────────────────────────────────────────┐
│                     GitAutoSync.GUI                      │
│  ┌──────────────┐  ┌─────────────┐  ┌───────────────┐  │
│  │ System Tray  │  │ Main Window │  │ HTTP/WS Client│  │
│  │   (Always)   │  │  (Optional) │  │               │  │
│  └──────┬───────┘  └─────────────┘  └───────┬───────┘  │
│         │                                     │          │
│         └─────────────────┬───────────────────┘          │
└───────────────────────────┼──────────────────────────────┘
                            │ HTTP REST Commands
                            │ WebSocket Updates (real-time)
                            ↓
┌─────────────────────────────────────────────────────────┐
│                   GitAutoSync.Daemon                     │
│  ┌──────────────┐  ┌─────────────┐  ┌───────────────┐  │
│  │ HTTP/WS      │  │ Repo Manager│  │ GitAutoSync   │  │
│  │ Server       │→ │             │→ │ Workers       │  │
│  │ (port 52847) │  │             │  │ (Core)        │  │
│  └──────────────┘  └─────────────┘  └───────────────┘  │
└─────────────────────────────────────────────────────────┘
                            │
                            ↓
                    ┌───────────────┐
                    │ GitAutoSync   │
                    │ .Core         │
                    │ (Shared Lib)  │
                    └───────────────┘
```

### Project Structure

```
GitAutoSync/
├── GitAutoSync.Core/              (Shared library - .NET 10)
│   ├── GitAutoSyncDirectoryWorker.cs
│   ├── StartupManager.cs
│   ├── Config/
│   └── Models/
│
├── GitAutoSync.Daemon/            (NEW - Standalone daemon - .NET 10)
│   ├── Program.cs
│   ├── DaemonServer.cs           (Kestrel HTTP/WebSocket server)
│   ├── RepositoryManager.cs      (Manages workers)
│   ├── Api/
│   │   ├── RepositoriesController.cs
│   │   ├── ConfigController.cs
│   │   ├── StatusController.cs
│   │   └── WebSocketHandler.cs   (Real-time updates)
│   └── GitAutoSync.Daemon.csproj
│
├── GitAutoSync.GUI/               (System tray + UI - .NET 10)
│   ├── TrayIcon/
│   │   ├── TrayIconManager.cs
│   │   └── TrayMenu.cs
│   ├── Services/
│   │   ├── DaemonClient.cs       (HTTP REST client)
│   │   ├── DaemonWebSocketClient.cs
│   │   └── DaemonLifecycle.cs    (Start/stop daemon)
│   ├── ViewModels/
│   ├── Views/
│   └── GitAutoSync.GUI.csproj
│
└── GitAutoSync.Console/           (Optional CLI tool - .NET 10)
    └── GitAutoSync.Console.csproj
```

## Design Details

### 1. Daemon (GitAutoSync.Daemon)

**Responsibilities:**
- Host Kestrel HTTP server on localhost:52847
- Manage repository workers (start/stop/status)
- Load and manage configuration
- Send real-time updates via WebSocket
- Support both embedded (subprocess) and standalone (service) modes

**API Endpoints:**

HTTP REST:
- `GET /health` - Health check (used by UI to detect daemon)
- `POST /api/repositories` - Add repository
- `DELETE /api/repositories/{id}` - Remove repository
- `POST /api/repositories/{id}/start` - Start monitoring
- `POST /api/repositories/{id}/stop` - Stop monitoring
- `POST /api/repositories/start-all` - Start all repositories
- `POST /api/repositories/stop-all` - Stop all repositories
- `GET /api/repositories` - Get all repositories with status
- `POST /api/config/load` - Load config file
- `POST /api/config/save` - Save config file
- `GET /api/status` - Get daemon status

WebSocket:
- `/ws` - Real-time updates (logs, status changes, events)

**Message Format (WebSocket):**
```json
{
  "type": "log" | "status" | "error",
  "timestamp": "ISO8601",
  "data": {
    "repository": "repo-name",
    "level": "INFO" | "WARNING" | "ERROR",
    "message": "..."
  }
}
```

### 2. UI (GitAutoSync.GUI)

**Responsibilities:**
- System tray icon with menu (always visible)
- Main window (optional, show/hide)
- Auto-start daemon if not running
- Send commands to daemon via HTTP
- Receive real-time updates via WebSocket
- Thin client - all state lives in daemon

**System Tray Features:**
- Icon shows running status (green/gray)
- Right-click menu:
  - Show/Hide Window
  - Start All / Stop All
  - Start on Login (toggle)
  - Quit
- Double-click to show window

**UI Data Flow:**
- User adds repo → HTTP POST → Daemon persists
- User starts monitoring → HTTP POST → Daemon starts worker
- Daemon sends log → WebSocket → UI displays in real-time
- UI closes → Daemon keeps running
- UI reopens → WebSocket reconnects, gets current state

**Daemon Lifecycle (DaemonLifecycle.cs):**
1. On startup: HTTP health check to localhost:52847
2. If healthy: Connect WebSocket, fetch current state
3. If unhealthy: Start daemon as subprocess
4. On quit: Option to stop daemon or leave running

### 3. Core Library (GitAutoSync.Core)

**Unchanged responsibilities:**
- GitAutoSyncDirectoryWorker - File watching and git operations
- StartupManager - Platform-specific auto-start (launchd/systemd/Windows)
- Config models and parsing

**Updates:**
- Upgrade to .NET 10
- Update all NuGet packages

### 4. Console (GitAutoSync.Console)

**Optional tool for power users:**
- Can talk to daemon via HTTP API
- Can run standalone monitoring (legacy mode)
- Useful for debugging and scripting

## Technology Choices

### HTTP/WebSocket Server
- **ASP.NET Core Kestrel** (built-in, mature, excellent WebSocket support)
- Minimal API for REST endpoints
- Native WebSocket support

### System Tray
- **Avalonia.TrayIcon** package (cross-platform, integrates with existing Avalonia UI)

### Daemon Detection
- **HTTP health check** on localhost:52847
- If fails → start daemon subprocess
- Simple, reliable, works with HTTP architecture

### Package Updates
- Update all NuGet packages to latest .NET 10 compatible versions
- Avalonia 11.x (check for 12.x if available)
- Serilog, ReactiveUI, CliWrap, etc.

## Auto-Start Behavior

**Scenario 1: First install**
- User enables "Start on Login"
- StartupManager creates platform entry (launchd/systemd/startup folder)
- Entry launches UI with `--minimized --auto-start`
- UI starts daemon, minimizes to tray

**Scenario 2: Login startup**
- Platform launches UI with `--minimized --auto-start`
- UI checks if daemon running
- If not: starts daemon subprocess
- UI goes to system tray (no window)
- Daemon auto-starts all configured repositories

**Scenario 3: Manual UI launch while daemon running**
- User double-clicks app
- UI detects daemon already running
- Connects via WebSocket
- Shows main window with current status

## Error Handling

**Daemon crashes:**
- UI detects via WebSocket disconnect
- UI shows notification
- UI attempts to restart daemon
- If restart fails repeatedly: show error dialog

**UI crashes:**
- Daemon keeps running
- Repositories keep syncing
- User relaunches UI → reconnects seamlessly

**Port conflict (52847 in use):**
- Daemon tries alternative ports (52848, 52849, etc.)
- Writes actual port to `~/.git-auto-sync/daemon.port`
- UI reads port file before connecting

## Migration Path

**From current version:**
1. Config file format unchanged (TOML)
2. Repositories automatically migrate
3. First launch: UI starts daemon in subprocess mode
4. User can optionally install daemon as system service

## Testing Strategy

**Unit tests:**
- Core library functionality
- HTTP API endpoints
- WebSocket message handling

**Integration tests:**
- UI → Daemon communication
- Daemon lifecycle (start/stop/health)
- WebSocket reconnection

**Manual tests:**
- System tray behavior on all platforms
- Auto-start on login (macOS/Linux/Windows)
- Daemon survives UI crash
- UI reconnects to running daemon

## Open Questions

None - design approved, ready for implementation.

## Success Criteria

- [ ] All projects target .NET 10
- [ ] All NuGet packages updated
- [ ] Daemon runs standalone with HTTP/WebSocket API
- [ ] UI has system tray icon and menu
- [ ] UI auto-starts daemon if not running
- [ ] Real-time log streaming via WebSocket
- [ ] Auto-start on login works on macOS (primary platform)
- [ ] Daemon continues monitoring when UI closes
- [ ] UI reconnects cleanly to running daemon
