# Mirror ColonySeries/mods/* → <Game>/Mods/<Name>
param(
    [string]$GameModsDir = ""
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$Src = Join-Path $Root "mods"

if ($GameModsDir) {
    $Dest = $GameModsDir
} elseif ($env:GAME_MODS_DIR) {
    $Dest = $env:GAME_MODS_DIR
} elseif (Test-Path (Join-Path $Root "..\Mods")) {
    $Dest = (Resolve-Path (Join-Path $Root "..\Mods")).Path
} else {
    Write-Error "Pass -GameModsDir or set GAME_MODS_DIR"
}

Write-Host "Deploy $Src → $Dest"
New-Item -ItemType Directory -Force -Path $Dest | Out-Null

Get-ChildItem -Path $Src -Directory | ForEach-Object {
    $name = $_.Name
    $about = Join-Path $_.FullName "About\About.xml"
    if (-not (Test-Path $about)) {
        Write-Host "skip $name (no About.xml)"
        return
    }
    $target = Join-Path $Dest $name
    Write-Host "  sync $name"
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    # robocopy mirror: /MIR deletes extras in dest for this folder only
    & robocopy $_.FullName $target /MIR /XD obj bin .vs _decomp .git /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
    # robocopy exit codes 0-7 are success-ish
    if ($LASTEXITCODE -ge 8) {
        Write-Error "robocopy failed for $name code=$LASTEXITCODE"
    }
}

Write-Host "Done. Enable mods in RimWorld → Mods list."
