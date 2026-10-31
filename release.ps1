[CmdletBinding()]
param(
    [ValidateRange(1, 240)]
    [int]$StageTimeoutMinutes = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot
$artifactsRoot = Join-Path $repoRoot 'artifacts'
$bundleRoot = Join-Path $artifactsRoot 'YiZhiMaoDc.bundle'
$pluginProject = Join-Path $repoRoot 'src\YiZhiMao.Dc.Plugin\YiZhiMao.Dc.Plugin.csproj'
$coreTestsProject = Join-Path $repoRoot 'tests\YiZhiMao.Dc.Core.Tests\YiZhiMao.Dc.Core.Tests.csproj'
$matrixScript = Join-Path $repoRoot 'tests\autocad-version-matrix.ps1'
$bundleScript = Join-Path $repoRoot 'packaging\build-multiversion-bundle.ps1'
$installerProject = Join-Path $repoRoot 'packaging\Installer\YiZhiMaoDc.Installer.csproj'
$manifestSource = Join-Path $repoRoot 'packaging\YiZhiMaoDc.bundle\PackageContents.xml'

function ConvertTo-NativeArgument {
    param([Parameter(Mandatory = $true)][string]$Value)

    if ($Value.Contains('"')) {
        throw "Native process arguments cannot contain a quote: $Value"
    }

    if ($Value -notmatch '\s') {
        return $Value
    }

    return '"' + $Value + '"'
}

function Invoke-ReleaseStage {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$Arguments = @()
    )

    $argumentLine = ($Arguments | ForEach-Object { ConvertTo-NativeArgument $_ }) -join ' '
    Write-Host "==> $Name (timeout: $StageTimeoutMinutes minutes)..."
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $FilePath
    $startInfo.Arguments = $argumentLine
    $startInfo.WorkingDirectory = $repoRoot
    $startInfo.UseShellExecute = $false

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    $started = $false
    try {
        $started = $process.Start()
        if (-not $started) {
            throw "Could not start $Name."
        }

        $timeoutMilliseconds = [int][TimeSpan]::FromMinutes($StageTimeoutMinutes).TotalMilliseconds
        if (-not $process.WaitForExit($timeoutMilliseconds)) {
            & (Join-Path $env:SystemRoot 'System32\taskkill.exe') /PID $process.Id /T /F | Out-Host
            $taskkillExitCode = $LASTEXITCODE
            if ($taskkillExitCode -ne 0) {
                throw "$Name timed out, but taskkill failed with exit code $taskkillExitCode."
            }

            if (-not $process.WaitForExit(5000)) {
                throw "$Name timed out, and its process did not exit after taskkill reported success."
            }

            throw "$Name timed out after $StageTimeoutMinutes minutes. Its process tree was stopped."
        }

        if ($process.ExitCode -ne 0) {
            throw "$Name failed with exit code $($process.ExitCode)."
        }
    }
    finally {
        if ($started -and -not $process.HasExited) {
            & (Join-Path $env:SystemRoot 'System32\taskkill.exe') /PID $process.Id /T /F | Out-Host
            if ($LASTEXITCODE -ne 0 -or -not $process.WaitForExit(5000)) {
                Write-Warning "$Name cleanup could not confirm that the process tree exited."
            }
        }

        $process.Dispose()
        $stopwatch.Stop()
    }

    Write-Host ("==> {0} finished in {1:mm\:ss}." -f $Name, $stopwatch.Elapsed)
}

function Test-Bundle {
    param(
        [Parameter(Mandatory = $true)][string]$BundlePath,
        [Parameter(Mandatory = $true)][string]$ExpectedVersion
    )

    $manifestPath = Join-Path $BundlePath 'PackageContents.xml'
    $hashPath = Join-Path $BundlePath 'bundle-hashes.sha256'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Bundle manifest is missing: $manifestPath"
    }
    if (-not (Test-Path -LiteralPath $hashPath -PathType Leaf)) {
        throw "Bundle hash list is missing: $hashPath"
    }

    [xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8
    if ([string]$manifest.ApplicationPackage.AppVersion -ne $ExpectedVersion) {
        throw "Bundle version does not match $ExpectedVersion."
    }

    $requiredSeries = @(
        'R21.0', 'R22.0', 'R23.0', 'R23.1', 'R24.0', 'R24.1',
        'R24.2', 'R24.3', 'R25.0', 'R25.1', 'R26.0'
    )
    $manifestSeries = @($manifest.ApplicationPackage.Components.RuntimeRequirements | ForEach-Object {
        [string]$_.SeriesMin
    })
    foreach ($series in $requiredSeries) {
        if ($series -notin $manifestSeries) {
            throw "Bundle manifest does not contain $series."
        }

        $versionDirectory = Join-Path $BundlePath "Contents\$series"
        foreach ($assembly in @('YiZhiMao.Dc.Plugin.dll', 'YiZhiMao.Dc.Core.dll')) {
            if (-not (Test-Path -LiteralPath (Join-Path $versionDirectory $assembly) -PathType Leaf)) {
                throw "Bundle $series is missing $assembly."
            }
        }
    }

    $forbidden = @(Get-ChildItem -LiteralPath $BundlePath -File -Recurse | Where-Object {
        $_.Name -in @('AcCoreMgd.dll', 'AcDbMgd.dll', 'AcMgd.dll', 'AcWindows.dll', 'AdWindows.dll')
    })
    if ($forbidden.Count -gt 0) {
        throw "Autodesk runtime assemblies must not be redistributed: $($forbidden.FullName -join ', ')"
    }

    $bundleFullPath = [System.IO.Path]::GetFullPath($BundlePath)
    $listedFiles = New-Object 'System.Collections.Generic.HashSet[string]' (
        [StringComparer]::OrdinalIgnoreCase)
    foreach ($line in Get-Content -LiteralPath $hashPath -Encoding UTF8) {
        if ($line -notmatch '^([0-9a-f]{64}) \*(.+)$') {
            throw "Invalid SHA-256 list line: $line"
        }

        $expectedHash = $Matches[1]
        $normalizedRelativePath = $Matches[2].Replace('\', '/')
        if ([string]::IsNullOrWhiteSpace($normalizedRelativePath) -or
            [System.IO.Path]::IsPathRooted($normalizedRelativePath) -or
            $normalizedRelativePath.Split('/') -contains '..' -or
            -not $listedFiles.Add($normalizedRelativePath)) {
            throw "Invalid or duplicate SHA-256 path: $normalizedRelativePath"
        }

        $relativePath = $normalizedRelativePath.Replace('/', '\')
        $filePath = [System.IO.Path]::GetFullPath((Join-Path $bundleFullPath $relativePath))
        if (-not $filePath.StartsWith(
                $bundleFullPath + [System.IO.Path]::DirectorySeparatorChar,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "Unsafe SHA-256 path: $relativePath"
        }
        if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
            throw "SHA-256 list file is missing: $relativePath"
        }

        $actualHash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne $expectedHash) {
            throw "SHA-256 mismatch: $relativePath"
        }
    }

    foreach ($file in Get-ChildItem -LiteralPath $BundlePath -File -Recurse) {
        if ($file.FullName -eq $hashPath) {
            continue
        }

        $relativePath = $file.FullName.Substring($bundleFullPath.Length + 1).Replace('\', '/')
        if (-not $listedFiles.Contains($relativePath)) {
            throw "Bundle contains a file missing from the SHA-256 list: $relativePath"
        }
    }

    Write-Host 'Bundle integrity verification passed.' -ForegroundColor Green
}

function New-BundleZip {
    param(
        [Parameter(Mandatory = $true)][string]$BundlePath,
        [Parameter(Mandatory = $true)][string]$DestinationPath
    )

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path -LiteralPath $DestinationPath) {
        Remove-Item -LiteralPath $DestinationPath -Force
    }

    $bundleItem = Get-Item -LiteralPath $BundlePath
    $bundlePrefix = $bundleItem.FullName.TrimEnd('\') + '\'
    $stream = [System.IO.File]::Open(
        $DestinationPath,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::ReadWrite,
        [System.IO.FileShare]::None)
    $archive = New-Object System.IO.Compression.ZipArchive(
        $stream,
        [System.IO.Compression.ZipArchiveMode]::Create,
        $false)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $BundlePath -File -Recurse | Sort-Object FullName) {
            $relativePath = $file.FullName.Substring($bundlePrefix.Length).Replace('\', '/')
            $entryName = $bundleItem.Name + '/' + $relativePath
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive,
                $file.FullName,
                $entryName,
                [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $archive.Dispose()
        $stream.Dispose()
    }
}

foreach ($requiredPath in @(
    $pluginProject,
    $coreTestsProject,
    $matrixScript,
    $bundleScript,
    $installerProject,
    $manifestSource
)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required release input is missing: $requiredPath"
    }
}

[xml]$pluginProjectXml = Get-Content -LiteralPath $pluginProject -Raw -Encoding UTF8
$versionNode = Select-Xml -Xml $pluginProjectXml -XPath '/Project/PropertyGroup/Version' |
    Select-Object -First 1
$version = if ($null -eq $versionNode) { '' } else { [string]$versionNode.Node.InnerText }
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Plugin version must contain three numeric parts: $version"
}
[xml]$sourceManifest = Get-Content -LiteralPath $manifestSource -Raw -Encoding UTF8
if ([string]$sourceManifest.ApplicationPackage.AppVersion -ne $version) {
    throw "Plugin version $version does not match PackageContents.xml."
}

$dotnetPath = (Get-Command dotnet.exe -ErrorAction Stop).Source
$powerShellPath = (Get-Command powershell.exe -ErrorAction Stop).Source
$greenZip = Join-Path $artifactsRoot "YiZhiMaoDc-Green-v$version.zip"
$installerExe = Join-Path $artifactsRoot "YiZhiMaoDc-Setup-v$version.exe"

Write-Host "==> CADDC release v$version"
Invoke-ReleaseStage `
    -Name 'Core tests' `
    -FilePath $dotnetPath `
    -Arguments @('run', '--project', $coreTestsProject, '-c', 'Release')
Invoke-ReleaseStage `
    -Name 'AutoCAD 2017-2027 warning-free matrix' `
    -FilePath $powerShellPath `
    -Arguments @(
        '-NoLogo', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
        '-File', $matrixScript, '-Configuration', 'Release'
    )
Invoke-ReleaseStage `
    -Name 'Multi-version Bundle build' `
    -FilePath $powerShellPath `
    -Arguments @(
        '-NoLogo', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
        '-File', $bundleScript, '-Configuration', 'Release'
    )

Test-Bundle -BundlePath $bundleRoot -ExpectedVersion $version
New-BundleZip -BundlePath $bundleRoot -DestinationPath $greenZip

Invoke-ReleaseStage `
    -Name 'Installer build' `
    -FilePath $dotnetPath `
    -Arguments @(
        'build', $installerProject, '-c', 'Release', '-t:Rebuild', '--nologo',
        '-v:minimal', "-p:PayloadZip=$greenZip", "-p:Version=$version"
    )

$builtInstaller = Join-Path $repoRoot 'packaging\Installer\bin\Release\net462\YiZhiMaoDc-Setup.exe'
if (-not (Test-Path -LiteralPath $builtInstaller -PathType Leaf)) {
    throw "Installer build output is missing: $builtInstaller"
}
Copy-Item -LiteralPath $builtInstaller -Destination $installerExe -Force

$selfTestRoot = Join-Path $artifactsRoot ('.installer-selftest-' + [Guid]::NewGuid().ToString('N'))
try {
    Invoke-ReleaseStage `
        -Name 'Installer embedded Bundle self-test' `
        -FilePath $installerExe `
        -Arguments @('--selftest', $selfTestRoot)
    if ((Get-Content -LiteralPath (Join-Path $selfTestRoot 'selftest-result.txt') -Raw) -ne 'SELFTEST_OK') {
        throw 'Installer self-test did not produce SELFTEST_OK.'
    }
}
finally {
    if (Test-Path -LiteralPath $selfTestRoot) {
        Remove-Item -LiteralPath $selfTestRoot -Recurse -Force
    }
}

Write-Host ''
Write-Host 'CADDC release finished.' -ForegroundColor Green
foreach ($artifact in @($installerExe, $greenZip)) {
    $item = Get-Item -LiteralPath $artifact
    $hash = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash
    Write-Host "$($item.FullName)"
    Write-Host "  Size: $($item.Length) bytes"
    Write-Host "  SHA-256: $hash"
}
