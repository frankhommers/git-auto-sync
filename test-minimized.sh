#!/bin/bash

# Test script to verify the app works correctly when started minimized

echo "Building the application..."
dotnet build GitAutoSync.GUI/GitAutoSync.GUI.csproj

if [ $? -ne 0 ]; then
    echo "Build failed!"
    exit 1
fi

echo "Build successful!"
echo ""
echo "Testing the application with --minimized flag..."
echo "This will start the app minimized to test if repositories are populated."
echo ""
echo "To test:"
echo "1. Run: ./GitAutoSync.GUI/bin/Debug/net9.0/GitAutoSync.GUI --minimized --auto-start"
echo "2. Check if the repositories are loaded when you restore the window"
echo "3. Compare with running without --minimized flag"
echo ""
echo "The fix ensures that:"
echo "- Configuration loading happens immediately on startup"
echo "- UI initialization is triggered through multiple events"
echo "- Auto-start works regardless of window state"
echo ""
echo "Manual test required - please run the commands above and verify the behavior."
