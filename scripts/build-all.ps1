# Build every ColonySeries mod (Release).
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$ModsDir = Join-Path $Root "mods"

if ($env:RIMWORLD_DIR) {
    $RW = $env:RIMWORLD_DIR
} elseif (Test-Path (Join-Path $Root "..\RimWorldWin64_Data\Managed\Assembly-CSharp.dll")) {
    $RW = (Resolve-Path (Join-Path $Root "..")).Path
} else {
    Write-Error "Set RIMWORLD_DIR to your RimWorld install root."
}

Write-Host "RimWorldDir=$RW"
Write-Host "Mods source=$ModsDir"
Write-Host ""

$fail = 0
$built = 0
Get-ChildItem -Path $ModsDir -Recurse -Filter *.csproj | Sort-Object FullName | ForEach-Object {
    $name = $_.Directory.Name
    Write-Host "---- build $name ----"
    & dotnet build $_.FullName -c Release -p:RimWorldDir="$RW"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAILED: $($_.FullName)" -ForegroundColor Red
        $fail++
    } else {
        $built++
    }
    Write-Host ""
}

Write-Host "========"
Write-Host "built=$built  failed=$fail"
exit $fail
