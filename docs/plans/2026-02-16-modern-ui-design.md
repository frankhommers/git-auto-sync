# Modern UI Design - Git Auto Sync GUI

**Date:** 2026-02-16
**Approach:** Pure Fluent Theme (no extra packages)
**Theme:** System-following (dark/light)

## Layout

Sidebar layout replacing the current vertical stack:

- **Sidebar (280px fixed):** Repository list + Add/Start All/Stop All buttons
- **Content area (flexible):** Selected repository detail + activity log
- **Status bar (full width):** Status message + active/total counts

## Sidebar

- Background: `LayerFillColorDefaultBrush` (subtly different from content)
- Separated from content by a thin border
- "Add Repository" button at top
- "Start All" / "Stop All" buttons at bottom

### Repo Items

Each repo item shows:
- Status dot (8px `Ellipse`): green=running, grey=stopped, red=error
- Repo name (bold, primary text color)
- Path (smaller, secondary text color)
- Status + last activity on one line (secondary text color)

Selected item uses `SystemAccentColor` highlight. Hover via `PointerOver` triggers.

## Content Area

### Selected Repository Header

When a repo is selected:
- Repo name (large, bold)
- Full path (secondary color)
- Status indicator (dot + text + last activity)
- Action buttons: Start, Stop, Remove

When no repo selected: "Select a repository" placeholder.

### Activity Log

- Monospace font (`Cascadia Mono, Consolas, monospace`)
- Color-coded log levels (custom colors defined as app resources)
- Clear button in header
- Scrollable, max 1000 entries

## Color Strategy

Replace ALL hardcoded hex colors with theme resources:

| Purpose | Resource |
|---|---|
| Primary text | `TextFillColorPrimaryBrush` |
| Secondary text | `TextFillColorSecondaryBrush` |
| Card borders | `CardStrokeColorDefaultBrush` |
| Card backgrounds | `CardBackgroundFillColorDefaultBrush` |
| Sidebar background | `LayerFillColorDefaultBrush` |
| Subtle background | `LayerOnMicaBaseAltFillColorDefaultBrush` |

Custom status colors defined as app-level resources:
- `StatusRunningBrush`: `#48BB78` (green)
- `StatusErrorBrush`: `#F56565` (red)
- `StatusStoppedBrush`: `#718096` (grey)

## Dark Mode

`RequestedThemeVariant="Default"` in App.axaml (follows system).
All theme resources automatically adapt to dark/light.

## What Stays the Same

- No new NuGet packages
- ViewModels stay mostly unchanged (minor tweaks for selected repo detail display)
- All existing commands and bindings continue working
- No changes to daemon/core/services
- Functional behavior is identical
