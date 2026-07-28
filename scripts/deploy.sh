#!/usr/bin/env bash
# Mirror ColonySeries/mods/* → <Game>/Mods/<Name>
# Does NOT touch Steam workshop numeric folders.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SRC="$ROOT/mods"

if [[ -n "${1:-}" ]]; then
  DEST="$1"
elif [[ -n "${GAME_MODS_DIR:-}" ]]; then
  DEST="$GAME_MODS_DIR"
elif [[ -d "$ROOT/../Mods" ]]; then
  DEST="$(cd "$ROOT/../Mods" && pwd)"
else
  echo "Usage: $0 [GameModsDir]" >&2
  echo "  or set GAME_MODS_DIR" >&2
  exit 1
fi

echo "Deploy $SRC → $DEST"
mkdir -p "$DEST"

# Optional: only these names (default = all under mods/)
for dir in "$SRC"/*/; do
  [[ -d "$dir" ]] || continue
  name="$(basename "$dir")"
  # Skip if no About.xml (not a mod)
  if [[ ! -f "$dir/About/About.xml" ]]; then
    echo "skip $name (no About.xml)"
    continue
  fi
  target="$DEST/$name"
  echo "  sync $name"
  mkdir -p "$target"
  # Prefer rsync if present
  if command -v rsync >/dev/null 2>&1; then
    rsync -a --delete \
      --exclude '.git' \
      --exclude 'obj' \
      --exclude 'bin' \
      --exclude '_decomp' \
      --exclude '.vs' \
      "$dir" "$target/"
  else
    # tar mirror
    rm -rf "$target"
    mkdir -p "$target"
    (cd "$dir" && tar -cf - \
      --exclude='./obj' --exclude='./bin' --exclude='./.vs' --exclude='./_decomp' \
      --exclude='*/obj' --exclude='*/bin' \
      .) | (cd "$target" && tar -xf -)
  fi
done

echo "Done. Enable mods in RimWorld → Mods list."
