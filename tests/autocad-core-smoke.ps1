param(
    [string]$AutoCADDirectory = 'D:\Autodesk\AutoCAD 2026',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateRange(5, 600)]
    [int]$TimeoutSeconds = 60,
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repoRoot 'CADDC.sln'
$consolePath = Join-Path $AutoCADDirectory 'accoreconsole.exe'
$pluginPath = Join-Path $repoRoot "src\YiZhiMao.Dc.Plugin\bin\$Configuration\net8.0-windows\YiZhiMao.Dc.Plugin.dll"
$probeProjectPath = Join-Path $repoRoot 'tests\YiZhiMao.Dc.HostProbe\YiZhiMao.Dc.HostProbe.csproj'
$probePath = Join-Path $repoRoot "tests\YiZhiMao.Dc.HostProbe\bin\$Configuration\net8.0-windows\YiZhiMao.Dc.HostProbe.dll"
$fixturePath = Join-Path $repoRoot 'Modules\DimTemplate\DimTemplate.dwg'

foreach ($requiredPath in @($solutionPath, $consolePath, $fixturePath, $probeProjectPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required host-smoke input is missing: $requiredPath"
    }
}

if (-not $SkipBuild) {
    $autoCADProperty = "/p:AutoCADDirectory=$AutoCADDirectory"
    $buildOutput = @(& dotnet build $solutionPath -c $Configuration --nologo $autoCADProperty 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed before AcCoreConsole smoke test.`n$($buildOutput -join [Environment]::NewLine)"
    }

    $probeBuildOutput = @(& dotnet build $probeProjectPath -c $Configuration --nologo $autoCADProperty 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Host probe build failed before AcCoreConsole smoke test.`n$($probeBuildOutput -join [Environment]::NewLine)"
    }
}

foreach ($assemblyPath in @($pluginPath, $probePath)) {
    if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
        throw "Host-smoke assembly is missing: $assemblyPath"
    }
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$runDirectory = Join-Path $repoRoot "artifacts\host-smoke\$timestamp"
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
$scriptPath = Join-Path $runDirectory 'host-smoke.scr'
$stdoutPath = Join-Path $runDirectory 'stdout.txt'
$stderrPath = Join-Path $runDirectory 'stderr.txt'

$scriptLines = @(
    '_.FILEDIA',
    '0',
    '_.CMDECHO',
    '0',
    '_.SECURELOAD',
    '0',
    '_.TILEMODE',
    '1',
    '_.NETLOAD',
    $pluginPath.Replace('\', '/'),
    '_.NETLOAD',
    $probePath.Replace('\', '/'),
    'DC_CMD_STATUS',
    '_.LINE',
    '100000,102000',
    '101000,102000',
    '101000,102700',
    '100000,102700',
    '100000,102000',
    '',
    '_.RECTANG',
    '100000,102000',
    '101000,102700',
    '_.RECTANG',
    '100100,102100',
    '100900,102600',
    '_.RECTANG',
    '102000,102000',
    '103000,102700',
    '_.RECTANG',
    '100000,100000',
    '101000,100700',
    '_.ZOOM',
    '_E',
    'DC_TEST_FRAME_RECOGNITION',
    '_W',
    '99900,99900',
    '103100,102800',
    '',
    'DC_TEST_FRAME_CONTENT_GROUPING',
    '_.POINT',
    '-1000,-1000',
    '(setq dc_anchor (entlast))',
    'DC_CMD_BOARD_DRAW',
    '0,0',
    '1000,0',
    '(setq dc_plate (entnext dc_anchor))',
    '(if (and dc_plate (= "LWPOLYLINE" (cdr (assoc 0 (entget dc_plate)))) (= 4 (cdr (assoc 90 (entget dc_plate)))) (= 1 (logand 1 (cdr (assoc 70 (entget dc_plate)))))) (princ "\nDC_HOST_GEOMETRY_PASS") (princ "\nDC_HOST_GEOMETRY_FAIL"))',
    '(setq dc_detail_before (entlast))',
    'DC_CMD_DETAIL_DRAW',
    '-10,-10',
    '1010,30',
    '2000,0',
    '(setq dc_detail_after (entlast))',
    '(if (and (/= dc_detail_before dc_detail_after) (entget dc_detail_after)) (princ "\nDC_HOST_DETAIL_PASS") (princ "\nDC_HOST_DETAIL_FAIL"))',
    '(setq dc_clip_before (entlast))',
    'DC_CMD_DETAIL_CLIP',
    '-10,-10',
    '1010,30',
    '4000,0',
    '(setq dc_clip_after (entlast))',
    '(if (and (/= dc_clip_before dc_clip_after) (entget dc_clip_after)) (princ "\nDC_HOST_CLIP_PASS") (princ "\nDC_HOST_CLIP_FAIL"))',
    '_.QUIT',
    '_Y'
)
[System.IO.File]::WriteAllLines($scriptPath, $scriptLines, [System.Text.Encoding]::ASCII)

$startInfo = New-Object System.Diagnostics.ProcessStartInfo
$startInfo.FileName = $consolePath
$startInfo.Arguments = '/i "{0}" /s "{1}" /l en-US /readonly' -f $fixturePath, $scriptPath
$startInfo.WorkingDirectory = $runDirectory
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.StandardOutputEncoding = [System.Text.Encoding]::Unicode
$startInfo.StandardErrorEncoding = [System.Text.Encoding]::Unicode

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $startInfo
[void]$process.Start()
$stdoutTask = $process.StandardOutput.ReadToEndAsync()
$stderrTask = $process.StandardError.ReadToEndAsync()

if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    try {
        $process.Kill()
        $process.WaitForExit()
    }
    catch {
        Write-Warning "Could not stop timed-out AcCoreConsole process $($process.Id): $($_.Exception.Message)"
    }

    throw "AcCoreConsole smoke test timed out after $TimeoutSeconds seconds. Run directory: $runDirectory"
}

$stdout = $stdoutTask.Result
$stderr = $stderrTask.Result
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($stdoutPath, $stdout, $utf8NoBom)
[System.IO.File]::WriteAllText($stderrPath, $stderr, $utf8NoBom)
$combinedOutput = $stdout + [Environment]::NewLine + $stderr
$outputLines = @($combinedOutput -split "`r?`n")

$failures = New-Object System.Collections.Generic.List[string]
if ($process.ExitCode -ne 0) {
    $failures.Add("AcCoreConsole exit code was $($process.ExitCode).")
}

if (-not ($outputLines | Where-Object { $_ -match '^\s*DC_HOST_GEOMETRY_PASS' })) {
    $failures.Add('Board geometry success marker was not found.')
}

if ($outputLines | Where-Object { $_ -match '^\s*DC_HOST_GEOMETRY_FAIL' }) {
    $failures.Add('Board geometry failure marker was found.')
}

if (-not ($outputLines | Where-Object { $_ -match '^\s*DC_HOST_DETAIL_PASS' })) {
    $failures.Add('Detail creation success marker was not found.')
}

if (-not ($outputLines | Where-Object { $_ -match '^\s*DC_HOST_CLIP_PASS' })) {
    $failures.Add('Detail clip success marker was not found.')
}

if ($outputLines | Where-Object { $_ -match '^\s*DC_HOST_(DETAIL|CLIP)_FAIL' }) {
    $failures.Add('Detail creation failure marker was found.')
}

$expectedFrameMarker = 'DC_HOST_FRAME_SCAN count=3 frames=100000,102000,101000,102700|102000,102000,103000,102700|100000,100000,101000,100700'
if (-not ($outputLines | Where-Object { $_.Trim() -eq $expectedFrameMarker })) {
    $failures.Add('Frame recognition count, deduplication, nesting, or reading order did not match the fixed sample.')
}

if (-not ($outputLines | Where-Object { $_ -match '^\s*DC_HOST_FRAME_CONTENT_PASS count=1' })) {
    $failures.Add('Overlapping block and ordinary geometry were not grouped into one frame candidate.')
}

if ($outputLines | Where-Object { $_ -match '^\s*DC_HOST_FRAME_CONTENT_(FAIL|ERROR)' }) {
    $failures.Add('Frame content grouping failure marker was found.')
}

foreach ($forbiddenPattern in @(
    'DC_HOST_FRAME_SCAN_ERROR',
    'DC_HOST_FRAME_CONTENT_ERROR',
    'Unknown command',
    '未知命令',
    'Unhandled exception',
    'eInvalidInput',
    'FATAL ERROR'
)) {
    if ($combinedOutput.IndexOf($forbiddenPattern, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        $failures.Add("Forbidden output was found: $forbiddenPattern")
    }
}

if ($failures.Count -gt 0) {
    throw (($failures -join [Environment]::NewLine) + "`nHost log: $stdoutPath`nError log: $stderrPath")
}

Write-Output 'AcCoreConsole host smoke test passed.'
Write-Output '  Loaded: YiZhiMao.Dc.Plugin.dll'
Write-Output '  Command: DC_CMD_STATUS'
Write-Output '  Geometry: DC_CMD_BOARD_DRAW -> closed 4-vertex polyline'
Write-Output '  Frames: 3 outer frames -> deduplicated, de-nested, reading order verified'
Write-Output '  Frame content: overlapping block and ordinary geometry -> one candidate'
Write-Output "  Log: $stdoutPath"
