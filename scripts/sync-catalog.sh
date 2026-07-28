#!/usr/bin/env bash
# Regenerate catalog.json from mods/*/About/About.xml
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
# Convert Git-Bash style /e/... to Windows E:/... for Python on Windows
ROOT_WIN="$ROOT"
if command -v cygpath >/dev/null 2>&1; then
  ROOT_WIN="$(cygpath -w "$ROOT")"
elif [[ "$ROOT" =~ ^/([a-zA-Z])/(.*)$ ]]; then
  ROOT_WIN="${BASH_REMATCH[1]^}:/${BASH_REMATCH[2]}"
fi
export COLONYSERIES_ROOT="$ROOT_WIN"
OUT="$ROOT/catalog.json"

python - <<'PY'
import json, re, os
from pathlib import Path
from datetime import date

root = Path(os.environ["COLONYSERIES_ROOT"])
mods_dir = root / "mods"
packs = []

def tag(text, name):
    m = re.search(rf"<{name}>(.*?)</{name}>", text, re.S)
    return m.group(1).strip() if m else None

for d in sorted(mods_dir.iterdir()):
    if not d.is_dir():
        continue
    about = d / "About" / "About.xml"
    if not about.exists():
        continue
    text = about.read_text(encoding="utf-8", errors="replace")
    csprojs = list(d.glob("**/1.6/Source/*/*.csproj"))
    needs_harmony = True
    if csprojs:
        c = csprojs[0].read_text(encoding="utf-8", errors="replace")
        m = re.search(r"<ColonySeriesNeedsHarmony>(.*?)</ColonySeriesNeedsHarmony>", c)
        if m:
            needs_harmony = m.group(1).strip().lower() == "true"
    dll = d / "1.6" / "Assemblies" / f"{d.name}.dll"
    packs.append({
        "folder": d.name,
        "name": tag(text, "name"),
        "packageId": tag(text, "packageId"),
        "author": tag(text, "author"),
        "needsHarmony": needs_harmony,
        "hasDll": dll.exists(),
        "hasCsproj": bool(csprojs),
        "hasDeviations": (d / "DEVIATIONS.md").exists(),
        "path": f"mods/{d.name}",
    })

doc = {
    "schema": 1,
    "series": "ColonySeries",
    "game": "RimWorld",
    "targetVersion": "1.6",
    "generated": str(date.today()),
    "packCount": len(packs),
    "packs": packs,
}
(root / "catalog.json").write_text(
    json.dumps(doc, indent=2, ensure_ascii=False) + "\n",
    encoding="utf-8",
)
print(f"Wrote {len(packs)} packs -> catalog.json")
PY
