#!/usr/bin/env bash
set -euo pipefail

APP_PATH="${1:-}"
DMG_PATH="${2:-}"
VOLUME_NAME="${3:-Git Auto Sync}"

if [[ -z "$APP_PATH" || -z "$DMG_PATH" ]]; then
  echo "Usage: scripts/create-dmg.sh <app-path> <dmg-path> [volume-name]" >&2
  exit 1
fi

if [[ ! -d "$APP_PATH" ]]; then
  echo "App bundle does not exist: $APP_PATH" >&2
  exit 1
fi

mkdir -p "$(dirname "$DMG_PATH")"
rm -f "$DMG_PATH"

hdiutil create \
  -volname "$VOLUME_NAME" \
  -srcfolder "$APP_PATH" \
  -ov \
  -format UDZO \
  "$DMG_PATH"

echo "Created DMG: $DMG_PATH"
