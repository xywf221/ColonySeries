#!/usr/bin/env bash
# Build every ColonySeries mod (Release).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MODS_DIR="$ROOT/mods"

# Game root: env > sibling of ColonySeries > explicit
# Directory.Build.props resolves RimWorldDir (sibling install or RIMWORLD_DIR).
# Still pass -p when we can detect the game, so logs are explicit.
RW_ARGS=()
if [[ -n "${RIMWORLD_DIR:-}" ]]; then
  RW="$RIMWORLD_DIR"
  # Ensure trailing separator for older csproj concat
  [[ "$RW" == */ ]] || [[ "$RW" == *\\ ]] || RW="${RW}/"
  RW_ARGS=(-p:RimWorldDir="$RW")
  echo "RimWorldDir=$RW (from env)"
elif [[ -f "$ROOT/../RimWorldWin64_Data/Managed/Assembly-CSharp.dll" ]]; then
  RW="$(cd "$ROOT/.." && pwd)/"
  RW_ARGS=(-p:RimWorldDir="$RW")
  echo "RimWorldDir=$RW (sibling)"
else
  echo "RimWorldDir=auto (Directory.Build.props / per-csproj)"
fi
echo "Mods source=$MODS_DIR"
echo

fail=0
built=0
skipped=0

while IFS= read -r -d '' csproj; do
  name="$(basename "$(dirname "$csproj")")"
  echo "---- build $name ----"
  if dotnet build "$csproj" -c Release "${RW_ARGS[@]+"${RW_ARGS[@]}"}"; then
    built=$((built + 1))
  else
    echo "FAILED: $csproj" >&2
    fail=$((fail + 1))
  fi
  echo
done < <(find "$MODS_DIR" -name "*.csproj" -print0 | sort -z)

# Mods with no csproj (pure XML) — note only
while IFS= read -r -d '' dir; do
  base="$(basename "$dir")"
  if ! find "$dir" -name "*.csproj" | grep -q .; then
    echo "SKIP (no csproj): $base"
    skipped=$((skipped + 1))
  fi
done < <(find "$MODS_DIR" -mindepth 1 -maxdepth 1 -type d -print0 | sort -z)

echo "========"
echo "built=$built  failed=$fail  no-csproj-notes=$skipped"
exit "$fail"
