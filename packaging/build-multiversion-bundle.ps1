param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$AutoCAD2017Directory,
    [string]$AutoCAD2018Directory,
    [string]$AutoCAD2019Directory,
    [string]$AutoCAD2020Directory,
    [string]$AutoCAD2021Directory,
    [string]$AutoCAD2022Directory,
    [string]$AutoCAD2023Directory,
    [string]$AutoCAD2024Directory,
    [string]$AutoCAD2025Directory,
    [string]$AutoCAD2026Directory,
    [string]$AutoCAD2027Directory,
    [string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'src\YiZhiMao.Dc.Plugin\YiZhiMao.Dc.Plugin.csproj'
$modulesRoot = Join-Path $repoRoot 'Modules'
$manifestPath = Join-Path $PSScriptRoot 'YiZhiMaoDc.bundle\PackageContents.xml'
$siblingSdkRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot '..\DCPlugin\lib'))
$localSdkRoot = Join-Path $repoRoot 'lib\AutoCAD'

if ([string]::IsNullOrWhiteSpace($AutoCAD2017Directory)) {
    $AutoCAD2017Directory = Join-Path $localSdkRoot 'R21.0'
}
if ([string]::IsNullOrWhiteSpace($AutoCAD2018Directory)) {
    $AutoCAD2018Directory = Join-Path $localSdkRoot 'R22.0'
}
if ([string]::IsNullOrWhiteSpace($AutoCAD2019Directory)) {
    $AutoCAD2019Directory = Join-Path $localSdkRoot 'R23.0'
}
if ([string]::IsNullOrWhiteSpace($AutoCAD2020Directory)) {
    $AutoCAD2020Directory = Join-Path $localSdkRoot 'R23.1'
}
if ([string]::IsNullOrWhiteSpace($AutoCAD2021Directory)) {
    $AutoCAD2021Directory = Join-Path $localSdkRoot 'R24.0'
}
if ([string]::IsNullOrWhiteSpace($AutoCAD2022Directory)) {
    $AutoCAD2022Directory = Join-Path $localSdkRoot 'R24.1'
}
if ([string]::IsNullOrWhiteSpace($AutoCAD2023Directory)) {
    $AutoCAD2023Directory = Join-Path $localSdkRoot 'R24.2'
}

if ([string]::IsNullOrWhiteSpace($AutoCAD2024Directory)) {
    $AutoCAD2024Directory = Join-Path $siblingSdkRoot 'R24.0-R24.3_CAD_2021-2024'
}
if ([string]::IsNullOrWhiteSpace($AutoCAD2025Directory)) {
    $AutoCAD2025Directory = Join-Path $siblingSdkRoot 'R25.0_CAD_2025'
}
if ([string]::IsNullOrWhiteSpace($AutoCAD2026Directory)) {
    $AutoCAD2026Directory = Join-Path $siblingSdkRoot 'R25.1_CAD_2026'
}
if ([string]::IsNullOrWhiteSpace($AutoCAD2027Directory)) {
    $AutoCAD2027Directory = Join-Path $siblingSdkRoot 'R26.0_CAD_2027'
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts\YiZhiMaoDc.bundle'
}

$lanes = @(
    [pscustomobject]@{ Year = 2017; Series = 'R21.0'; Framework = 'net462'; Sdk = $AutoCAD2017Directory },
    [pscustomobject]@{ Year = 2018; Series = 'R22.0'; Framework = 'net462'; Sdk = $AutoCAD2018Directory },
    [pscustomobject]@{ Year = 2019; Series = 'R23.0'; Framework = 'net47'; Sdk = $AutoCAD2019Directory },
    [pscustomobject]@{ Year = 2020; Series = 'R23.1'; Framework = 'net47'; Sdk = $AutoCAD2020Directory },
    [pscustomobject]@{ Year = 2021; Series = 'R24.0'; Framework = 'net48'; Sdk = $AutoCAD2021Directory },
    [pscustomobject]@{ Year = 2022; Series = 'R24.1'; Framework = 'net48'; Sdk = $AutoCAD2022Directory },
    [pscustomobject]@{ Year = 2023; Series = 'R24.2'; Framework = 'net48'; Sdk = $AutoCAD2023Directory },
    [pscustomobject]@{ Year = 2024; Series = 'R24.3'; Framework = 'net48'; Sdk = $AutoCAD2024Directory },
    [pscustomobject]@{ Year = 2025; Series = 'R25.0'; Framework = 'net8.0-windows'; Sdk = $AutoCAD2025Directory },
    [pscustomobject]@{ Year = 2026; Series = 'R25.1'; Framework = 'net8.0-windows'; Sdk = $AutoCAD2026Directory },
    [pscustomobject]@{ Year = 2027; Series = 'R26.0'; Framework = 'net10.0-windows'; Sdk = $AutoCAD2027Directory }
)

$requiredReferences = @('AcCoreMgd.dll', 'AcDbMgd.dll', 'AcMgd.dll', 'AcWindows.dll', 'AdWindows.dll')
$missing = @($lanes | Where-Object {
    if ([string]::IsNullOrWhiteSpace($_.Sdk)) {
        return $true
    }

    foreach ($reference in $requiredReferences) {
        if (-not (Test-Path -LiteralPath (Join-Path $_.Sdk $reference) -PathType Leaf)) {
            return $true
        }
    }

    return $false
})
if ($missing.Count -gt 0) {
    $details = $missing | ForEach-Object { "AutoCAD $($_.Year) ($($_.Series)): $($_.Sdk)" }
    throw "Exact managed SDK references are required for every release lane. Missing:`n$($details -join [Environment]::NewLine)"
}

foreach ($lane in $lanes) {
    $expectedVersion = [Version]::Parse($lane.Series.Substring(1) + '.0.0')
    foreach ($reference in @('AcCoreMgd.dll', 'AcDbMgd.dll', 'AcMgd.dll', 'AcWindows.dll')) {
        $referencePath = Join-Path $lane.Sdk $reference
        $actualVersion = [System.Reflection.AssemblyName]::GetAssemblyName($referencePath).Version
        if ($actualVersion.Major -ne $expectedVersion.Major -or
            $actualVersion.Minor -ne $expectedVersion.Minor) {
            throw "AutoCAD $($lane.Year) SDK lane mismatch: $reference is $actualVersion, expected $($lane.Series)."
        }
    }
}

foreach ($requiredPath in @($projectPath, $modulesRoot, $manifestPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required build input is missing: $requiredPath"
    }
}

$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$bundleRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
if (-not $bundleRoot.StartsWith($artifactsRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must be inside $artifactsRoot"
}
if (Test-Path -LiteralPath $bundleRoot) {
    Remove-Item -LiteralPath $bundleRoot -Recurse -Force
}

$contentsRoot = Join-Path $bundleRoot 'Contents'
$bundleModules = Join-Path $contentsRoot 'Modules'
New-Item -ItemType Directory -Path $bundleModules -Force | Out-Null
Get-ChildItem -LiteralPath $modulesRoot -Recurse -File |
    Where-Object { $_.Extension -ne '.bak' } |
    ForEach-Object {
        $relativePath = $_.FullName.Substring($modulesRoot.Length).TrimStart('\')
        $destination = Join-Path $bundleModules $relativePath
        $destinationDirectory = Split-Path -Parent $destination
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
    }

foreach ($lane in $lanes) {
    Write-Output "Building AutoCAD $($lane.Year) ($($lane.Series), $($lane.Framework))..."
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
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "AutoCAD $($lane.Year) build failed with exit code $LASTEXITCODE."
    }

    $sourceDirectory = Join-Path $repoRoot "src\YiZhiMao.Dc.Plugin\bin\$Configuration\$($lane.Framework)"
    $versionDirectory = Join-Path $contentsRoot $lane.Series
    New-Item -ItemType Directory -Path $versionDirectory -Force | Out-Null
    Get-ChildItem -LiteralPath $sourceDirectory -File |
        Where-Object { $_.Extension -in @('.dll', '.pdb', '.json') } |
        Copy-Item -Destination $versionDirectory -Force

    foreach ($requiredAssembly in @('YiZhiMao.Dc.Plugin.dll', 'YiZhiMao.Dc.Core.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $versionDirectory $requiredAssembly) -PathType Leaf)) {
            throw "AutoCAD $($lane.Year) output is missing $requiredAssembly."
        }
    }

    $forbidden = @(Get-ChildItem -LiteralPath $versionDirectory -File | Where-Object {
        $_.Name -in @('AcCoreMgd.dll', 'AcDbMgd.dll', 'AcMgd.dll', 'AcWindows.dll', 'AdWindows.dll')
    })
    if ($forbidden.Count -gt 0) {
        throw "Autodesk runtime assemblies must not be redistributed: $($forbidden.Name -join ', ')"
    }
}

Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $bundleRoot 'PackageContents.xml') -Force

$hashPath = Join-Path $bundleRoot 'bundle-hashes.sha256'
$hashLines = Get-ChildItem -LiteralPath $bundleRoot -Recurse -File |
    Where-Object { $_.FullName -ne $hashPath } |
    Sort-Object FullName |
    ForEach-Object {
        $relativePath = $_.FullName.Substring($bundleRoot.Length).TrimStart('\').Replace('\', '/')
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash *$relativePath"
    }
$utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllLines($hashPath, $hashLines, $utf8WithoutBom)

Write-Output "Multi-version bundle created: $bundleRoot"
Write-Output 'Validated lanes: AutoCAD 2017 through AutoCAD 2027.'
