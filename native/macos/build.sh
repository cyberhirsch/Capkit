#!/bin/bash
# Builds libcapkit_mac.dylib (universal: Apple Silicon + Intel) into the given output folder.
# Usage: native/macos/build.sh <output-dir>
set -euo pipefail

OUT="${1:-$(dirname "$0")/build}"
SRC="$(cd "$(dirname "$0")" && pwd)/CapkitMac.swift"
WORK="$(mktemp -d)"
mkdir -p "$OUT"

for arch in arm64 x86_64; do
    swiftc -O -swift-version 5 -emit-library -module-name CapkitMac \
        -target "$arch-apple-macos14.0" \
        -framework ScreenCaptureKit -framework CoreGraphics \
        -o "$WORK/libcapkit_mac-$arch.dylib" "$SRC"
done

lipo -create "$WORK/libcapkit_mac-arm64.dylib" "$WORK/libcapkit_mac-x86_64.dylib" -output "$OUT/libcapkit_mac.dylib"
rm -rf "$WORK"
echo "Built $OUT/libcapkit_mac.dylib"
