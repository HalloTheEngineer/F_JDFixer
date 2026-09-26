#!/usr/bin/env bash
#
# Packages the reference assemblies JDFixer.csproj needs into a single zip, laid out the way the
# project expects, so CI can build without a Beat Saber install.
#
# Usage:  tools/pack-refs.sh <beat-saber-dir> [output.zip]
#
#   <beat-saber-dir>   a Beat Saber installation, e.g.
#                      ~/.local/share/BSManager/BSInstances/1.44.1
#
# The result is a faithful reproduction of the 19 assemblies referenced by JDFixer.csproj:
#
#   Beat Saber_Data/Managed/*.dll   16 game assemblies
#   Libs/0Harmony.dll
#   Plugins/BSML.dll, Plugins/SiraUtil.dll
#
# WHY THIS IS A SCRIPT AND NOT A COMMIT
#
# Those files are Beat Games' and the mod dependencies' copyrighted binaries. They must not be
# committed to this repository or attached to a public release. The bundle is meant to be stripped
# first (ProjectSIRA/Suto or beat-forge/GenericStripper both do this) and then uploaded somewhere
# private - a private release asset, an S3 bucket, a private repo - with the URL given to CI as the
# BS_REFS_URL secret.
#
# There is deliberately no public fallback. beat-forge/beatsaber-stripped is the usual source for
# this, but it currently stops at 1.42.0 and this mod targets 1.44.1, so CI cannot fetch refs for the
# version that actually matters.

set -euo pipefail

GAME_DIR="${1:-}"
OUT="${2:-bs-refs.zip}"

if [[ -z "$GAME_DIR" ]]; then
    echo "usage: $0 <beat-saber-dir> [output.zip]" >&2
    exit 2
fi

GAME_DIR="${GAME_DIR%/}"
MANAGED="$GAME_DIR/Beat Saber_Data/Managed"

# Resolved up front: the zip is written from a temporary directory, so a relative path would end up
# relative to that directory rather than to where the caller ran the script from.
case "$OUT" in
    /*) ;;
    *) OUT="$PWD/$OUT" ;;
esac

if [[ ! -d "$MANAGED" ]]; then
    echo "error: '$MANAGED' does not exist - is '$GAME_DIR' a Beat Saber install?" >&2
    exit 1
fi

# Kept in one place so this stays in step with the <Reference> items in JDFixer.csproj.
MANAGED_DLLS=(
    BGLib.UnityExtension
    BGLib.AppFlow
    BeatSaber.ViewSystem
    BeatmapCore
    Core
    DataModels
    GameplayCore
    HMUI
    IPA.Loader
    Main
    Unity.TextMeshPro
    UnityEngine.CoreModule
    UnityEngine.UI
    UnityEngine.UIModule
    Zenject
    Zenject-usage
)
LIB_DLLS=(0Harmony)
PLUGIN_DLLS=(BSML SiraUtil)

missing=()
for name in "${MANAGED_DLLS[@]}"; do
    [[ -f "$MANAGED/$name.dll" ]] || missing+=("Beat Saber_Data/Managed/$name.dll")
done
for name in "${LIB_DLLS[@]}"; do
    [[ -f "$GAME_DIR/Libs/$name.dll" ]] || missing+=("Libs/$name.dll")
done
for name in "${PLUGIN_DLLS[@]}"; do
    [[ -f "$GAME_DIR/Plugins/$name.dll" ]] || missing+=("Plugins/$name.dll")
done

if (( ${#missing[@]} )); then
    echo "error: missing ${#missing[@]} reference(s) in '$GAME_DIR':" >&2
    printf '  %s\n' "${missing[@]}" >&2
    exit 1
fi

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

outdir="$work/Refs"
mkdir -p "$outdir/Beat Saber_Data/Managed" "$outdir/Libs" "$outdir/Plugins"

for name in "${MANAGED_DLLS[@]}"; do
    cp "$MANAGED/$name.dll" "$outdir/Beat Saber_Data/Managed/"
done
for name in "${LIB_DLLS[@]}"; do
    cp "$GAME_DIR/Libs/$name.dll" "$outdir/Libs/"
done
for name in "${PLUGIN_DLLS[@]}"; do
    cp "$GAME_DIR/Plugins/$name.dll" "$outdir/Plugins/"
done

# A manifest so a bundle can be traced back to what produced it. Hashes let CI notice that somebody
# repointed BS_REFS_URL at a different set of assemblies.
game_version="unknown"
[[ -f "$GAME_DIR/BeatSaberVersion.txt" ]] && game_version="$(tr -d '\r\n' < "$GAME_DIR/BeatSaberVersion.txt")"

{
    echo "{"
    echo "  \"gameVersion\": \"$game_version\","
    echo "  \"sourceDirectory\": \"$GAME_DIR\","
    echo "  \"files\": {"
    first=1
    while IFS= read -r f; do
        rel="${f#"$outdir/"}"
        [[ $first -eq 1 ]] || echo ","
        first=0
        printf '    "%s": "%s"' "$rel" "$(sha256sum "$f" | cut -d' ' -f1)"
    done < <(find "$outdir" -name '*.dll' | sort)
    echo ""
    echo "  }"
    echo "}"
} > "$outdir/refs.json"

rm -f "$OUT"
(cd "$work" && zip -qr "$OUT" Refs)

echo "wrote $OUT ($(du -h "$OUT" | cut -f1))"
echo "game version: $game_version"
echo
echo "Next: strip it, upload it somewhere private, then set the repository secret"
echo "  BS_REFS_URL to its download URL."
