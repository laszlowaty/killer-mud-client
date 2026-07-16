#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

# ---- read version from Directory.Build.props ----
VERSION=$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' Directory.Build.props)
VERSION="${VERSION:-0.1.0}"

# ---- parameter: beta / release (default: release) ----
FLAVOR="${1:-release}"
case "$FLAVOR" in
  beta)   SUFFIX="-beta" ;;
  release) SUFFIX="" ;;
  *)      echo "Usage: $0 [beta|release]" >&2; exit 1 ;;
esac

PROJECT="src/MudClient.App/MudClient.App.csproj"
APP_NAME="KillerMudClient-$VERSION$SUFFIX"

detect_platform() {
  case "$(uname -s)" in
    Linux)  echo "linux-x64" ;;
    Darwin)
      case "$(uname -m)" in
        arm64) echo "osx-arm64" ;;
        x86_64) echo "osx-x64" ;;
      esac
      ;;
    *) echo "Unsupported platform: $(uname -s)" >&2; exit 1 ;;
  esac
}

RID=$(detect_platform)
ARCH="${RID/osx-/}"
ARCH="${ARCH/linux-/}"
OUTDIR="$ROOT/publish/$RID/$FLAVOR"

echo "============================================================"
echo " MudClient.App  $VERSION  ($FLAVOR)"
echo " Platform:      $RID"
echo " Output:        $OUTDIR"
echo " App:           $APP_NAME"
echo "============================================================"
echo ""

dotnet publish "$PROJECT" \
    --configuration Release \
    --runtime "$RID" \
    --self-contained true \
    --output "$OUTDIR" \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    -p:Version="$VERSION"

# ---- rename output to versioned name ----
if [ -f "$OUTDIR/MudClient.App" ]; then
  mv "$OUTDIR/MudClient.App" "$OUTDIR/$APP_NAME"
fi
if [ -f "$OUTDIR/MudClient.App.exe" ]; then
  mv "$OUTDIR/MudClient.App.exe" "$OUTDIR/$APP_NAME.exe"
fi

# Single-file publish can leave native debug symbols from dependencies behind.
find "$OUTDIR" -mindepth 1 -maxdepth 1 \
  ! -name "$APP_NAME" \
  ! -name "$APP_NAME.exe" \
  -exec rm -rf -- {} +

chmod +x "$OUTDIR/$APP_NAME" 2>/dev/null || true

echo ""
echo "============================================================"
echo " Publish successful."
echo " Executable: $OUTDIR/$APP_NAME"
echo "============================================================"
echo ""
echo " Usage: $0 [beta|release]"
