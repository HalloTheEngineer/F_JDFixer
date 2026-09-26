#!/usr/bin/env bash
#
# Downloads and unpacks the reference assemblies produced by tools/pack-refs.sh.
#
# Usage:  tools/fetch-refs.sh <output-dir>
#
# Reads BS_REFS_URL (required) and BS_REFS_TOKEN (optional, sent as a bearer token).
# Verifies the bundle is complete and was built for the game version this mod targets, because a
# mismatched bundle otherwise shows up as a wall of CS0246 "type or namespace not found" errors that
# look like source problems.

set -euo pipefail

DEST="${1:-}"
MANIFEST_VERSION="${BS_EXPECTED_GAME_VERSION:-}"

if [[ -z "$DEST" ]]; then
    echo "usage: $0 <output-dir>" >&2
    exit 2
fi

if [[ -z "${BS_REFS_URL:-}" ]]; then
    cat >&2 <<'EOF'
error: BS_REFS_URL is not set.

  This build needs the Beat Saber reference assemblies, and they cannot be fetched
  automatically for 1.44.1: beat-forge/beatsaber-stripped currently stops at 1.42.0,
  and the Beat Games assemblies are not ours to redistribute.

  To fix this, once:

    1. On a machine that owns Beat Saber 1.44.1, run
         tools/pack-refs.sh <beat-saber-dir> bs-refs.zip
    2. Strip it (ProjectSIRA/Suto or beat-forge/GenericStripper) so it contains no
       game code - it must never be committed or attached to a public release.
    3. Upload it somewhere private: a private release asset, an S3 bucket, a private repo.
    4. Add a repository secret BS_REFS_URL with its download URL, and BS_REFS_TOKEN if
       the URL needs a bearer token.
EOF
    exit 1
fi

rm -rf "$DEST"
mkdir -p "$DEST"

archive="$(mktemp -t bs-refs-XXXXXX.zip)"
trap 'rm -f "$archive"' EXIT

echo "Downloading reference assemblies..."
if [[ -n "${BS_REFS_TOKEN:-}" ]]; then
    curl --fail --silent --show-error --location \
         --header "Authorization: Bearer ${BS_REFS_TOKEN}" \
         --output "$archive" "$BS_REFS_URL"
else
    curl --fail --silent --show-error --location --output "$archive" "$BS_REFS_URL"
fi

echo "Unpacking into $DEST ..."
unzip -q "$archive" -d "$DEST"

# pack-refs.sh wraps everything in a top-level Refs/ directory.
game_dir="$DEST"
if [[ -d "$DEST/Refs" ]]; then
    game_dir="$DEST/Refs"
fi

# The 19 assemblies JDFixer.csproj references. If one is absent the build fails with a missing-file
# error that says nothing useful, so check up front.
MANAGED_DLLS=(
    BGLib.UnityExtension BGLib.AppFlow BeatSaber.ViewSystem BeatmapCore Core
    DataModels GameplayCore HMUI IPA.Loader Main Unity.TextMeshPro
    UnityEngine.CoreModule UnityEngine.UI UnityEngine.UIModule Zenject Zenject-usage
)
missing=()
for name in "${MANAGED_DLLS[@]}"; do
    [[ -f "$game_dir/Beat Saber_Data/Managed/$name.dll" ]] || missing+=("Beat Saber_Data/Managed/$name.dll")
done
for name in 0Harmony; do
    [[ -f "$game_dir/Libs/$name.dll" ]] || missing+=("Libs/$name.dll")
done
for name in BSML SiraUtil; do
    [[ -f "$game_dir/Plugins/$name.dll" ]] || missing+=("Plugins/$name.dll")
done

if (( ${#missing[@]} )); then
    echo "error: the reference bundle is incomplete, missing ${#missing[@]} file(s):" >&2
    printf '  %s\n' "${missing[@]}" >&2
    exit 1
fi

if [[ -f "$game_dir/refs.json" ]]; then
    bundle_version="$(sed -n 's/.*"gameVersion"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$game_dir/refs.json" | head -1)"
    echo "Bundle built for Beat Saber: ${bundle_version:-unknown}"

    if [[ -n "$MANIFEST_VERSION" && -n "$bundle_version" && "$bundle_version" != "$MANIFEST_VERSION"* ]]; then
        cat >&2 <<EOF
error: reference bundle is for '$bundle_version' but manifest.json targets '$MANIFEST_VERSION'.

  Building against the wrong version produces missing-type errors that look like source
  bugs. Re-run tools/pack-refs.sh against a $MANIFEST_VERSION install.
EOF
        exit 1
    fi
fi

echo "All 19 reference assemblies present."
echo "GameDirectory=$game_dir"
