#!/bin/bash

# Test script for launch agent functionality
# This script demonstrates how the launch agent will work

echo "Testing Git Auto Sync GUI launch agent functionality..."

# Path to the built executable
GUI_PATH="/Users/frankhommers/Repos/git-auto-sync/GitAutoSync.GUI/bin/Release/net9.0/publish/GitAutoSync.GUI"
CONFIG_PATH="/Users/frankhommers/Repos/git-auto-sync/GitAutoSync.Console/GitAutoSync.toml"

echo "Executable path: $GUI_PATH"
echo "Config path: $CONFIG_PATH"

# Test if executable exists
if [ ! -f "$GUI_PATH" ]; then
    echo "ERROR: Executable not found. Run 'dotnet publish GitAutoSync.GUI -c Release' first."
    exit 1
fi

# Test if config exists
if [ ! -f "$CONFIG_PATH" ]; then
    echo "ERROR: Config file not found at $CONFIG_PATH"
    exit 1
fi

echo "Testing command that would be used by launch agent:"
echo "$GUI_PATH --config-file \"$CONFIG_PATH\" --minimized"

echo ""
echo "To set up the launch agent:"
echo "1. Run the GUI application"
echo "2. Load the config file: $CONFIG_PATH"
echo "3. Click 'Setup Launch Agent' button"
echo ""
echo "The launch agent will be created at:"
echo "~/Library/LaunchAgents/tools.franks.git-auto-sync-gui.plist"
echo ""
echo "Logs will be written to:"
echo "~/Library/Logs/GitAutoSync/GitAutoSync-GUI.log"
echo "~/Library/Logs/GitAutoSync/GitAutoSync-GUI.Error.log"
