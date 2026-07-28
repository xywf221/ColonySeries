#!/usr/bin/env bash
# Print packageId + path for every mod in the monorepo.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
printf "%-18s %-36s %s\n" "FOLDER" "PACKAGE ID" "NAME"
printf "%-18s %-36s %s\n" "------" "----------" "----"
for d in "$ROOT/mods"/*/; do
  [[ -f "$d/About/About.xml" ]] || continue
  folder="$(basename "$d")"
  pid=$(sed -n 's/.*<packageId>\([^<]*\)<\/packageId>.*/\1/p' "$d/About/About.xml" | head -1)
  name=$(sed -n 's/.*<name>\([^<]*\)<\/name>.*/\1/p' "$d/About/About.xml" | head -1)
  printf "%-18s %-36s %s\n" "$folder" "$pid" "$name"
done
