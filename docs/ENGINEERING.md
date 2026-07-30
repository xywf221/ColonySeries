# Engineering handbook

How this monorepo is built, validated, and shipped. Design intent lives in
`docs/design/`; this file is the **build system contract**.

## Goals

1. One clone → build all 14 mods without editing drive letters.
2. One folder = one RimWorld `packageId` = one Mods-list row.
3. Shared MSBuild; per-mod csproj is **identity only**.
4. Deploy mirrors into `<Game>/Mods/<Name>` without touching Steam workshop IDs.
5. Soft-link between series packs; never hard-wire the whole suite as one dependency.

## Layout contract

```
ColonySeries/
  Directory.Build.props      shared TFM, RimWorldDir, Harmony search
  Directory.Build.targets    shared refs + validate target
  mods/<Name>/
    About/About.xml           packageId, author, description
    About/Manifest.xml        optional Fluffy Manifest
    LoadFolders.xml
    README.md
    DEVIATIONS.md             optional
    1.6/
      Assemblies/<Name>.dll
      Defs/ Languages/ …
      Source/<Name>/<Name>.csproj   ← identity only
      Source/<Name>/*.cs
    Textures/
  scripts/
  docs/
  catalog.json                machine-readable pack list
```

### csproj identity template

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>Foo</AssemblyName>
    <RootNamespace>Foo</RootNamespace>
    <ColonySeriesNeedsHarmony>true</ColonySeriesNeedsHarmony>
  </PropertyGroup>
</Project>
```

Set `ColonySeriesNeedsHarmony` to `false` only when the pack has **zero**
`HarmonyLib` / `Harmony(` usage (today: TraitExtractor).

**Do not** re-declare `TargetFramework`, `OutputPath`, or game references in
the csproj — they come from `Directory.Build.*`.

## RimWorldDir resolution order

1. `-p:RimWorldDir=...`
2. env `RIMWORLD_DIR`
3. Sibling of `ColonySeries/` that contains `RimWorldWin64_Data/Managed/Assembly-CSharp.dll`
4. Climb four levels from the csproj (deployed `<Game>/Mods/Name/1.6/Source/Name` layout)

Trailing slash is normalized. Missing `Assembly-CSharp` fails the build with a
clear error (see `ColonySeries_ValidateRimWorldRefs`).

## Harmony resolution order

When `ColonySeriesNeedsHarmony=true`:

1. `-p:HarmonyPath=...`
2. `Mods/2009463077/Current/Assemblies/0Harmony.dll` (Steam workshop common)
3. `Mods/brrainz.harmony/...`
4. `Mods/Harmony/...`
5. NuGet `Lib.Harmony` 2.3.6 (compile-only, `PrivateAssets=all`)

Runtime always loads the player’s Harmony mod — we never ship `0Harmony.dll`
inside our Assemblies.

## Scripts

| Script | Purpose |
|--------|---------|
| `scripts/build-all.sh\|.ps1` | Release-build every csproj |
| `scripts/deploy.sh\|.ps1` | Mirror `mods/*` → game `Mods/` |
| `scripts/validate-mods.sh\|.ps1` | Structural + convention checks |
| `scripts/new-mod.sh` | Scaffold a new pack from template |
| `scripts/list-mods.sh` | packageId table |
| `scripts/sync-catalog.sh` | Regenerate `catalog.json` from About.xml |

Exit codes: `0` ok, non-zero = failed packs / violations.

## Validation rules (`validate-mods`)

For every `mods/<Name>/`:

| Check | Severity |
|-------|----------|
| `About/About.xml` present with `name`, `author`, `packageId`, `supportedVersions` | error |
| `packageId` unique across repo | error |
| `LoadFolders.xml` present | error |
| `README.md` present | error |
| `1.6/Assemblies/<Name>.dll` present **or** csproj present | error |
| If csproj: `AssemblyName` matches folder / dll name | error |
| `ColonySeriesNeedsHarmony` matches actual Harmony usage in `.cs` | warn→error |
| No `map.AllCells` in `.cs` (series convention) | error |
| English + ChineseSimplified Keyed if any Keyed exists | warn |
| Author is `ColonySeries` (branding) | warn |

## Versioning

- Game target: **RimWorld 1.6** only for now (`supportedVersions` / LoadFolders `v1.6`).
- Pack version: `About/Manifest.xml` `<version>` when present; otherwise document in CHANGELOG.
- Series release tags: `series-YYYY.MM.DD` or semver `v0.x.y` on the monorepo.

## Adding a mod

```bash
./scripts/new-mod.sh CoolWidget coolwidget.feature
# edit mods/CoolWidget/...
dotnet build mods/CoolWidget/1.6/Source/CoolWidget/CoolWidget.csproj -c Release
./scripts/validate-mods.sh
./scripts/sync-catalog.sh
./scripts/deploy.sh
```

## Deploy

Default destination: `../Mods` (sibling of `ColonySeries`).

```bash
./scripts/deploy.sh
# or
./scripts/deploy.ps1 -GameModsDir "D:\Games\RimWorld\Mods"
```

Uses rsync/robocopy **mirror** per named folder. Numeric Steam IDs are never
touched.

## What “engineering grade” means here

- Reproducible builds (deterministic, shared props)
- One-command CI-shaped validate + build
- Documented contracts (this file + CONTRIBUTING)
- Catalog as data, not only markdown tables
- No workshop junk in git; source of truth is `mods/`

Not in scope (yet): automated in-game smoke, multiplayer API, Steam Workshop
publish pipeline.
