param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot "src\YiZhiMao.Dc.Plugin\YiZhiMao.Dc.Plugin.csproj"

$buildOutput = & dotnet build $projectPath -c $Configuration --no-restore -v:minimal 2>&1
$buildExitCode = $LASTEXITCODE
if ($buildExitCode -ne 0) {
    $buildOutput | ForEach-Object { Write-Output $_ }
    throw "Plugin build failed with exit code $buildExitCode."
}

$conflicts = @($buildOutput | Where-Object {
    $line = $_.ToString()
    ($line.Contains("MSB3277") -or $line.Contains("MSB3243")) -and
    ($line.Contains("Microsoft.VisualBasic") -or $line.Contains("System.Drawing"))
})

if ($conflicts.Count -gt 0) {
    $conflicts | Select-Object -Unique | ForEach-Object { Write-Output $_ }
    throw "AutoCAD reference conflicts are present."
}

Write-Output "AutoCAD reference conflict check passed."
