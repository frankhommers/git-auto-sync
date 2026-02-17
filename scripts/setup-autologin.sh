#!/usr/bin/env bash
set -euo pipefail

PLIST_PATH="$HOME/Library/LaunchAgents/tools.franks.git-auto-sync-gui.plist"
APP_PATH="/Applications/Git Auto Sync.app"
CONFIG_PATH=""

usage() {
  cat <<EOF
Creates/removes a LaunchAgent for Git Auto Sync GUI auto-login on macOS.

Usage:
  scripts/setup-autologin.sh enable --config <path-to-config.toml> [--app <path-to-app-bundle>]
  scripts/setup-autologin.sh disable
EOF
}

if [[ $# -lt 1 || "$1" == "-h" || "$1" == "--help" ]]; then
  usage
  exit 0
fi

COMMAND="$1"
shift

while [[ $# -gt 0 ]]; do
  case "$1" in
    --config)
      CONFIG_PATH="$2"
      shift 2
      ;;
    --app)
      APP_PATH="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage
      exit 1
      ;;
  esac
done

case "$COMMAND" in
  enable)
    if [[ -z "$CONFIG_PATH" ]]; then
      echo "Missing required --config argument" >&2
      exit 1
    fi
    if [[ ! -f "$CONFIG_PATH" ]]; then
      echo "Config file does not exist: $CONFIG_PATH" >&2
      exit 1
    fi

    GUI_EXEC="$APP_PATH/Contents/MacOS/GitAutoSync.GUI"
    if [[ ! -x "$GUI_EXEC" ]]; then
      echo "GUI executable not found: $GUI_EXEC" >&2
      exit 1
    fi

    mkdir -p "$(dirname "$PLIST_PATH")"
    cat > "$PLIST_PATH" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key>
  <string>tools.franks.git-auto-sync-gui</string>
  <key>ProgramArguments</key>
  <array>
    <string>$GUI_EXEC</string>
    <string>--config-file</string>
    <string>$CONFIG_PATH</string>
    <string>--auto-start</string>
  </array>
  <key>RunAtLoad</key>
  <true/>
  <key>KeepAlive</key>
  <false/>
</dict>
</plist>
EOF

    launchctl unload "$PLIST_PATH" >/dev/null 2>&1 || true
    launchctl load "$PLIST_PATH"
    echo "Auto-login enabled via $PLIST_PATH"
    ;;

  disable)
    launchctl unload "$PLIST_PATH" >/dev/null 2>&1 || true
    rm -f "$PLIST_PATH"
    echo "Auto-login disabled"
    ;;

  *)
    echo "Unknown command: $COMMAND" >&2
    usage
    exit 1
    ;;
esac
