#!/bin/bash
# Wraps a `dotnet publish` output folder into Capkit.app and signs it ad hoc.
# Usage: native/macos/package-app.sh <publish-dir> <output-dir> <version>
set -euo pipefail

PUBLISH="$1"
OUT="$2"
VERSION="${3:-0.0.0}"
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
APP="$OUT/Capkit.app"

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

# The whole .NET app lives next to the executable; Contents/MacOS is where macOS looks for it.
cp -R "$PUBLISH/." "$APP/Contents/MacOS/"
rm -f "$APP/Contents/MacOS/Portable"
cp "$ROOT/Branding/Capkit.icns" "$APP/Contents/Resources/Capkit.icns"

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>Capkit</string>
    <key>CFBundleDisplayName</key>
    <string>Capkit</string>
    <key>CFBundleIdentifier</key>
    <string>io.github.cyberhirsch.capkit</string>
    <key>CFBundleExecutable</key>
    <string>Capkit</string>
    <key>CFBundleIconFile</key>
    <string>Capkit</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>$VERSION</string>
    <key>CFBundleVersion</key>
    <string>$VERSION</string>
    <key>LSMinimumSystemVersion</key>
    <string>14.0</string>
    <key>LSApplicationCategoryType</key>
    <string>public.app-category.productivity</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSMicrophoneUsageDescription</key>
    <string>Capkit records your microphone when you include it in a screen recording.</string>
    <key>NSHumanReadableCopyright</key>
    <string>Copyright (c) 2007-2026 ShareX Team. Capkit is licensed under the GNU GPL.</string>
</dict>
</plist>
PLIST

chmod +x "$APP/Contents/MacOS/Capkit"

# Apple Silicon refuses to run unsigned code; an ad hoc signature is enough to launch locally.
codesign --force --deep --sign - "$APP"
codesign --verify --deep --strict "$APP"
echo "Built $APP ($VERSION)"
