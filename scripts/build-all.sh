#!/usr/bin/env bash
# Build every ColonySeries mod (Release).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MODS_DIR="$ROOT/mods"

# Game root: env > sibling of ColonySeries > explicit
if [[ -n "${RIMWORLD_DIR:-}" ]]; then
  RW="$RIMWORLD_DIR"
elif [[ -f "$ROOT/../RimWorldWin64_Data/Managed/Assembly-CSharp.dll" ]]; then
  RW="$(cd "$ROOT/.." && pwd)"
elif [[ -f "$ROOT/../RimWorldWin64.exe" ]] || [[ -d "$ROOT/../RimWorldWin64_Data" ]]; then
  RW="$(cd "$ROOT/.." && pwd)"
else
  echo "Set RIMWORLD_DIR to your RimWorld install root (folder with RimWorldWin64_Data)." >&2
  exit 1
fi

echo "RimWorldDir=$RW"
echo "Mods source=$MODS_DIR"
echo

fail=0
built=0
skipped=0

while IFS= read -r -d '' csproj; do
  name="$(basename "$(dirname "$csproj")")"
  echo "---- build $name ----"
  if dotnet build "$csproj" -c Release -p:RimWorldDir="$RW"; then
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
