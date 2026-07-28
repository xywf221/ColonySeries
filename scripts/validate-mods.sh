#!/usr/bin/env bash
# Structural + convention checks for every pack under mods/.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MODS="$ROOT/mods"
errors=0
warns=0

err()  { echo "ERROR: $*" >&2; errors=$((errors + 1)); }
warn() { echo "WARN:  $*" >&2; warns=$((warns + 1)); }
ok()   { echo "OK:    $*"; }

declare -A SEEN_PID

xml_tag() {
  # xml_tag <file> <tag> -> first text content
  local f="$1" t="$2"
  sed -n "s/.*<${t}>\\([^<]*\\)<\\/${t}>.*/\\1/p" "$f" 2>/dev/null | head -1
}

for dir in "$MODS"/*/; do
  [[ -d "$dir" ]] || continue
  name="$(basename "$dir")"
  echo "---- $name ----"

  about="$dir/About/About.xml"
  if [[ ! -f "$about" ]]; then
    err "$name: missing About/About.xml"
    continue
  fi

  pkg=$(xml_tag "$about" packageId)
  author=$(xml_tag "$about" author)
  modname=$(xml_tag "$about" name)
  [[ -n "$pkg" ]] || err "$name: About.xml missing <packageId>"
  [[ -n "$modname" ]] || err "$name: About.xml missing <name>"
  [[ -n "$author" ]] || err "$name: About.xml missing <author>"

  if [[ -n "$pkg" ]]; then
    if [[ -n "${SEEN_PID[$pkg]:-}" ]]; then
      err "$name: duplicate packageId '$pkg' (also ${SEEN_PID[$pkg]})"
    else
      SEEN_PID[$pkg]="$name"
    fi
  fi

  if grep -q "<li>1.6</li>" "$about" || grep -q ">1.6<" "$about"; then
    :
  else
    err "$name: About.xml supportedVersions missing 1.6"
  fi

  if [[ "$author" != "ColonySeries" ]]; then
    warn "$name: author is '$author' (expected ColonySeries)"
  fi

  [[ -f "$dir/LoadFolders.xml" ]] || err "$name: missing LoadFolders.xml"
  [[ -f "$dir/README.md" ]] || err "$name: missing README.md"

  csproj=$(find "$dir" -name "*.csproj" | head -1 || true)
  dll="$dir/1.6/Assemblies/${name}.dll"
  if [[ -z "$csproj" && ! -f "$dll" ]]; then
    err "$name: no csproj and no 1.6/Assemblies/${name}.dll"
  fi

  if [[ -n "$csproj" ]]; then
    an=$(sed -n 's/.*<AssemblyName>\([^<]*\)<\/AssemblyName>.*/\1/p' "$csproj" | head -1)
    if [[ -n "$an" && "$an" != "$name" ]]; then
      err "$name: AssemblyName '$an' != folder '$name'"
    fi
    needs=$(sed -n 's/.*<ColonySeriesNeedsHarmony>\([^<]*\)<\/ColonySeriesNeedsHarmony>.*/\1/p' "$csproj" | head -1)
    needs=${needs:-true}
    has_harmony=0
    if rg -q "HarmonyLib|new Harmony\(|HarmonyPatch" "$dir" --glob "*.cs" 2>/dev/null; then
      has_harmony=1
    fi
    if [[ "$needs" == "true" && "$has_harmony" -eq 0 ]]; then
      warn "$name: ColonySeriesNeedsHarmony=true but no Harmony usage in .cs"
    fi
    if [[ "$needs" == "false" && "$has_harmony" -eq 1 ]]; then
      err "$name: ColonySeriesNeedsHarmony=false but Harmony usage found"
    fi
  fi

  # Code-only (skip // and /// comments). Flag real member access.
  if rg -n "\.AllCells\b" "$dir" --glob "*.cs" 2>/dev/null \
      | rg -v '^\S+:\d+:\s*//' \
      | rg -v '^\S+:\d+:\s*/\*' \
      | head -5 \
      | grep -q .; then
    rg -n "\.AllCells\b" "$dir" --glob "*.cs" 2>/dev/null \
      | rg -v '^\S+:\d+:\s*//' \
      | rg -v '^\S+:\d+:\s*/\*' \
      | head -5 || true
    err "$name: map.AllCells usage found (series convention forbids hot-path AllCells)"
  fi

  if [[ -d "$dir/1.6/Languages" ]]; then
    if [[ -d "$dir/1.6/Languages/English" && ! -d "$dir/1.6/Languages/ChineseSimplified" ]]; then
      warn "$name: English languages present but no ChineseSimplified"
    fi
    if [[ -d "$dir/1.6/Languages/ChineseSimplified" && ! -d "$dir/1.6/Languages/English" ]]; then
      warn "$name: ChineseSimplified present but no English"
    fi
  fi

  # Empty texture dirs are fine; missing required ContentFinder paths are runtime.
  ok "$name packageId=$pkg"
done

echo "========"
echo "errors=$errors  warnings=$warns"
if [[ "$errors" -gt 0 ]]; then
  exit 1
fi
exit 0
