#!/usr/bin/env bash
# Scaffold a new ColonySeries pack.
# Usage: ./scripts/new-mod.sh CoolWidget coolwidget.feature ["Display Name"] [harmony=true|false]
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
FOLDER="${1:-}"
PACKAGE_ID="${2:-}"
DISPLAY_NAME="${3:-$FOLDER}"
NEEDS_HARMONY="${4:-true}"

if [[ -z "$FOLDER" || -z "$PACKAGE_ID" ]]; then
  echo "Usage: $0 <FolderName> <package.id> [Display Name] [true|false harmony]" >&2
  exit 1
fi

DEST="$ROOT/mods/$FOLDER"
if [[ -e "$DEST" ]]; then
  echo "Already exists: $DEST" >&2
  exit 1
fi

mkdir -p "$DEST/About" \
         "$DEST/1.6/Assemblies" \
         "$DEST/1.6/Defs" \
         "$DEST/1.6/Languages/English/Keyed" \
         "$DEST/1.6/Languages/ChineseSimplified/Keyed" \
         "$DEST/1.6/Source/$FOLDER" \
         "$DEST/Textures"

cat > "$DEST/About/About.xml" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<ModMetaData>
  <name>${DISPLAY_NAME}</name>
  <author>ColonySeries</author>
  <packageId>${PACKAGE_ID}</packageId>
  <supportedVersions>
    <li>1.6</li>
  </supportedVersions>
  <description>
${DISPLAY_NAME} — ColonySeries pack. Replace this description.
  </description>
  <modDependencies>
    <li>
      <packageId>brrainz.harmony</packageId>
      <displayName>Harmony</displayName>
      <steamWorkshopUrl>steam://url/CommunityFilePage/2009463077</steamWorkshopUrl>
    </li>
  </modDependencies>
  <loadAfter>
    <li>brrainz.harmony</li>
    <li>Ludeon.RimWorld</li>
  </loadAfter>
</ModMetaData>
EOF

if [[ "$NEEDS_HARMONY" != "true" ]]; then
  # strip harmony dependency block for pure packs — keep simple: rewrite without deps
  cat > "$DEST/About/About.xml" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<ModMetaData>
  <name>${DISPLAY_NAME}</name>
  <author>ColonySeries</author>
  <packageId>${PACKAGE_ID}</packageId>
  <supportedVersions>
    <li>1.6</li>
  </supportedVersions>
  <description>
${DISPLAY_NAME} — ColonySeries pack. Replace this description.
  </description>
  <loadAfter>
    <li>Ludeon.RimWorld</li>
  </loadAfter>
</ModMetaData>
EOF
fi

cat > "$DEST/About/Manifest.xml" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<Manifest>
  <identifier>${PACKAGE_ID}</identifier>
  <version>0.1.0</version>
  <loadAfter>
    <li>brrainz.harmony</li>
  </loadAfter>
</Manifest>
EOF

cat > "$DEST/LoadFolders.xml" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<loadFolders>
  <v1.6>
    <li>/</li>
    <li>1.6</li>
  </v1.6>
</loadFolders>
EOF

cat > "$DEST/1.6/Source/$FOLDER/$FOLDER.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Identity only. Shared build lives in ColonySeries/Directory.Build.* -->
  <PropertyGroup>
    <AssemblyName>${FOLDER}</AssemblyName>
    <RootNamespace>${FOLDER}</RootNamespace>
    <ColonySeriesNeedsHarmony>${NEEDS_HARMONY}</ColonySeriesNeedsHarmony>
  </PropertyGroup>
</Project>
EOF

NS="$FOLDER"
cat > "$DEST/1.6/Source/$FOLDER/${FOLDER}Mod.cs" <<EOF
using Verse;

namespace ${NS}
{
    public class ${FOLDER}Mod : Mod
    {
        public static ${FOLDER}Settings Settings;

        public ${FOLDER}Mod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<${FOLDER}Settings>();
        }

        public override string SettingsCategory() => "${DISPLAY_NAME}";

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("Enable ${DISPLAY_NAME}", ref Settings.modEnabled);
            listing.End();
            base.DoSettingsWindowContents(inRect);
        }
    }

    public class ${FOLDER}Settings : ModSettings
    {
        public bool modEnabled = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref modEnabled, "modEnabled", true);
        }
    }
}
EOF

KEY="${FOLDER}"
cat > "$DEST/1.6/Languages/English/Keyed/${FOLDER}_Keys.xml" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<LanguageData>
  <!-- Add keyed translations for ${FOLDER} -->
</LanguageData>
EOF

cat > "$DEST/1.6/Languages/ChineseSimplified/Keyed/${FOLDER}_Keys.xml" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<LanguageData>
  <!-- 为 ${FOLDER} 添加键值翻译 -->
</LanguageData>
EOF

cat > "$DEST/README.md" <<EOF
# ${DISPLAY_NAME}

ColonySeries pack (\`${PACKAGE_ID}\`).

## Build

\`\`\`bash
dotnet build mods/${FOLDER}/1.6/Source/${FOLDER}/${FOLDER}.csproj -c Release
\`\`\`

## Design

Fill in player verb, cost, success/fail feedback, easy mode, sleep conditions.
See \`docs/design/mod-design-review.md\`.
EOF

cat > "$DEST/DEVIATIONS.md" <<EOF
# ${FOLDER} deviations

None yet (scaffold).
EOF

# Add to solution if present
if [[ -f "$ROOT/ColonySeries.slnx" ]]; then
  dotnet sln "$ROOT/ColonySeries.slnx" add "$DEST/1.6/Source/$FOLDER/$FOLDER.csproj" 2>/dev/null || true
elif [[ -f "$ROOT/ColonySeries.sln" ]]; then
  dotnet sln "$ROOT/ColonySeries.sln" add "$DEST/1.6/Source/$FOLDER/$FOLDER.csproj" 2>/dev/null || true
fi

echo "Scaffolded mods/${FOLDER}"
echo "Next: implement design → build → validate-mods → deploy"
