# Build every ColonySeries mod (Release).
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$ModsDir = Join-Path $Root "mods"

$buildArgs = @("-c", "Release")
if ($env:RIMWORLD_DIR) {
    $RW = $env:RIMWORLD_DIR
    if (-not $RW.EndsWith("\") -and -not $RW.EndsWith("/")) { $RW += "\" }
    $buildArgs += "-p:RimWorldDir=$RW"
    Write-Host "RimWorldDir=$RW (from env)"
} elseif (Test-Path (Join-Path $Root "..\RimWorldWin64_Data\Managed\Assembly-CSharp.dll")) {
    $RW = (Resolve-Path (Join-Path $Root "..")).Path
    if (-not $RW.EndsWith("\") -and -not $RW.EndsWith("/")) { $RW += "\" }
    $buildArgs += "-p:RimWorldDir=$RW"
    Write-Host "RimWorldDir=$RW (sibling)"
} else {
    Write-Host "RimWorldDir=auto (Directory.Build.props / per-csproj)"
}

Write-Host "Mods source=$ModsDir"
Write-Host ""

$fail = 0
$built = 0
Get-ChildItem -Path $ModsDir -Recurse -Filter *.csproj | Sort-Object FullName | ForEach-Object {
    $name = $_.Directory.Name
    Write-Host "---- build $name ----"
    & dotnet build $_.FullName @buildArgs
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
