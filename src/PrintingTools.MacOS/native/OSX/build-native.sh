#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT="$SCRIPT_DIR/PrintingToolsMacBridge.dylib"

if [[ "$(uname)" != "Darwin" ]]; then
  echo "Skipping native bridge build on non-macOS host." >&2
  exit 0
fi

clang -fobjc-arc -ObjC -shared -fobjc-link-runtime \
  -framework AppKit \
  -framework Foundation \
  -framework PDFKit \
  "$SCRIPT_DIR/PrintingToolsMacBridge.mm" \
  -o "$OUTPUT"

# Copy into the sample app so it can run without an additional build step.
SAMPLE_RUNTIME_DIR="$(cd "$SCRIPT_DIR"/../../../../samples/AvaloniaSample && pwd)/runtimes/osx/native"
mkdir -p "$SAMPLE_RUNTIME_DIR"
cp -f "$OUTPUT" "$SAMPLE_RUNTIME_DIR/PrintingToolsMacBridge.dylib"
