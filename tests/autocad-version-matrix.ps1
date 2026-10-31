param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'src\YiZhiMao.Dc.Plugin\YiZhiMao.Dc.Plugin.csproj'
$manifestPath = Join-Path $repoRoot 'packaging\YiZhiMaoDc.bundle\PackageContents.xml'
$sdkRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot '..\DCPlugin\lib'))
$localSdkRoot = Join-Path $repoRoot 'lib\AutoCAD'
$lanes = @(
    [pscustomobject]@{ Year = 2017; Series = 'R21.0'; Framework = 'net462'; Sdk = Join-Path $localSdkRoot 'R21.0' },
    [pscustomobject]@{ Year = 2018; Series = 'R22.0'; Framework = 'net462'; Sdk = Join-Path $localSdkRoot 'R22.0' },
    [pscustomobject]@{ Year = 2019; Series = 'R23.0'; Framework = 'net47'; Sdk = Join-Path $localSdkRoot 'R23.0' },
    [pscustomobject]@{ Year = 2020; Series = 'R23.1'; Framework = 'net47'; Sdk = Join-Path $localSdkRoot 'R23.1' },
    [pscustomobject]@{ Year = 2021; Series = 'R24.0'; Framework = 'net48'; Sdk = Join-Path $localSdkRoot 'R24.0' },
    [pscustomobject]@{ Year = 2022; Series = 'R24.1'; Framework = 'net48'; Sdk = Join-Path $localSdkRoot 'R24.1' },
    [pscustomobject]@{ Year = 2023; Series = 'R24.2'; Framework = 'net48'; Sdk = Join-Path $localSdkRoot 'R24.2' },
    [pscustomobject]@{ Year = 2024; Series = 'R24.3'; Framework = 'net48'; Sdk = Join-Path $sdkRoot 'R24.0-R24.3_CAD_2021-2024' },
    [pscustomobject]@{ Year = 2025; Series = 'R25.0'; Framework = 'net8.0-windows'; Sdk = Join-Path $sdkRoot 'R25.0_CAD_2025' },
    [pscustomobject]@{ Year = 2026; Series = 'R25.1'; Framework = 'net8.0-windows'; Sdk = Join-Path $sdkRoot 'R25.1_CAD_2026' },
    [pscustomobject]@{ Year = 2027; Series = 'R26.0'; Framework = 'net10.0-windows'; Sdk = Join-Path $sdkRoot 'R26.0_CAD_2027' }
)
$requiredReferences = @('AcCoreMgd.dll', 'AcDbMgd.dll', 'AcMgd.dll', 'AcWindows.dll', 'AdWindows.dll')

[xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8
$series = @($manifest.ApplicationPackage.Components.RuntimeRequirements | ForEach-Object { $_.SeriesMin })
foreach ($requiredSeries in @('R21.0', 'R22.0', 'R23.0', 'R23.1', 'R24.0', 'R24.1', 'R24.2', 'R24.3', 'R25.0', 'R25.1', 'R26.0')) {
    if ($requiredSeries -notin $series) {
        throw "PackageContents.xml does not contain an exact lane for $requiredSeries."
    }
}

foreach ($lane in $lanes) {
    $missingReferences = @($requiredReferences | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $lane.Sdk $_) -PathType Leaf)
    })
    if ($missingReferences.Count -gt 0) {
        throw "Reference assemblies are missing for AutoCAD $($lane.Year): $($missingReferences -join ', ') in $($lane.Sdk)"
    }

    $expectedVersion = [Version]::Parse($lane.Series.Substring(1) + '.0.0')
    foreach ($reference in @('AcCoreMgd.dll', 'AcDbMgd.dll', 'AcMgd.dll', 'AcWindows.dll')) {
        $referencePath = Join-Path $lane.Sdk $reference
        $actualVersion = [System.Reflection.AssemblyName]::GetAssemblyName($referencePath).Version
        if ($actualVersion.Major -ne $expectedVersion.Major -or
            $actualVersion.Minor -ne $expectedVersion.Minor) {
            throw "AutoCAD $($lane.Year) SDK lane mismatch: $reference is $actualVersion, expected $($lane.Series)."
        }
    }

    $arguments = @(
        'build', $projectPath,
        '-c', $Configuration,
        '-f', $lane.Framework,
        '--nologo',
        '--no-incremental',
        '-p:BuildAllAutoCADVersions=true',
        '-p:BuildBundle=false',
        "-p:AutoCADDirectory=$($lane.Sdk)"
    )
    $output = @(& dotnet @arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        $output | ForEach-Object { Write-Output $_ }
        throw "AutoCAD $($lane.Year) matrix build failed."
    }

    $warningLines = @($output | Where-Object {
        $_.ToString() -match '(?i):\s*warning\s+[A-Z]{2,}\d+\b|警告\s+[A-Z]{2,}\d+\b'
    })
    if ($warningLines.Count -gt 0) {
        $warningLines | ForEach-Object { Write-Output $_ }
        throw "AutoCAD $($lane.Year) matrix build produced warnings."
    }

    Write-Output "PASS AutoCAD $($lane.Year) $($lane.Series) $($lane.Framework)"
}

Write-Output 'AutoCAD 2017-2027 SDK matrix passed with zero warnings.'
