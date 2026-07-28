# Structural + convention checks (PowerShell twin of validate-mods.sh)
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$Mods = Join-Path $Root "mods"
$errors = 0
$warns = 0
$seen = @{}

function Err($m) { Write-Host "ERROR: $m" -ForegroundColor Red; $script:errors++ }
function Warn($m) { Write-Host "WARN:  $m" -ForegroundColor Yellow; $script:warns++ }
function Ok($m) { Write-Host "OK:    $m" -ForegroundColor Green }

function Get-XmlTag([string]$file, [string]$tag) {
    if (-not (Test-Path $file)) { return $null }
    $t = Get-Content -Raw $file
    if ($t -match "<$tag>([^<]*)</$tag>") { return $Matches[1] }
    return $null
}

Get-ChildItem $Mods -Directory | ForEach-Object {
    $name = $_.Name
    Write-Host "---- $name ----"
    $about = Join-Path $_.FullName "About\About.xml"
    if (-not (Test-Path $about)) { Err "$name missing About/About.xml"; return }

    $pkg = Get-XmlTag $about "packageId"
    $author = Get-XmlTag $about "author"
    $modname = Get-XmlTag $about "name"
    if (-not $pkg) { Err "$name missing packageId" }
    if (-not $modname) { Err "$name missing name" }
    if (-not $author) { Err "$name missing author" }
    if ($pkg) {
        if ($seen.ContainsKey($pkg)) { Err "$name duplicate packageId $pkg (also $($seen[$pkg]))" }
        else { $seen[$pkg] = $name }
    }
    $aboutText = Get-Content -Raw $about
    if ($aboutText -notmatch "1\.6") { Err "$name About.xml missing 1.6 support" }
    if ($author -and $author -ne "ColonySeries") { Warn "$name author is '$author' (expected ColonySeries)" }

    if (-not (Test-Path (Join-Path $_.FullName "LoadFolders.xml"))) { Err "$name missing LoadFolders.xml" }
    if (-not (Test-Path (Join-Path $_.FullName "README.md"))) { Err "$name missing README.md" }

    $csproj = Get-ChildItem $_.FullName -Recurse -Filter *.csproj -ErrorAction SilentlyContinue | Select-Object -First 1
    $dll = Join-Path $_.FullName "1.6\Assemblies\$name.dll"
    if (-not $csproj -and -not (Test-Path $dll)) { Err "$name no csproj and no dll" }

    if ($csproj) {
        $csp = Get-Content -Raw $csproj.FullName
        if ($csp -match "<AssemblyName>([^<]+)</AssemblyName>" -and $Matches[1] -ne $name) {
            Err "$name AssemblyName $($Matches[1]) != folder $name"
        }
        $needs = "true"
        if ($csp -match "<ColonySeriesNeedsHarmony>([^<]+)</ColonySeriesNeedsHarmony>") { $needs = $Matches[1] }
        $hasH = Select-String -Path (Join-Path $_.FullName "**\*.cs") -Pattern "HarmonyLib|new Harmony\(|HarmonyPatch" -ErrorAction SilentlyContinue
        # fallback simple scan
        $hasHarmony = $false
        Get-ChildItem $_.FullName -Recurse -Filter *.cs | ForEach-Object {
            if ((Get-Content -Raw $_.FullName) -match "HarmonyLib|HarmonyPatch|new Harmony\(") { $script:hasHarmony = $true }
        }
        if ($needs -eq "true" -and -not $hasHarmony) { Warn "$name NeedsHarmony=true but no Harmony usage" }
        if ($needs -eq "false" -and $hasHarmony) { Err "$name NeedsHarmony=false but Harmony usage found" }
    }

    Get-ChildItem $_.FullName -Recurse -Filter *.cs -ErrorAction SilentlyContinue | ForEach-Object {
        if ((Get-Content -Raw $_.FullName) -match "\.AllCells\b|map\.AllCells") {
            Err "$name map.AllCells usage in $($_.Name)"
        }
    }

    Ok "$name packageId=$pkg"
}

Write-Host "========"
Write-Host "errors=$errors  warnings=$warns"
if ($errors -gt 0) { exit 1 }
exit 0
