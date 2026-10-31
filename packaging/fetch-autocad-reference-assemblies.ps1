param(
    [switch]$AcceptAutodeskLicense
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $AcceptAutodeskLicense) {
    throw 'ObjectARX files are Autodesk proprietary software. Re-run with -AcceptAutodeskLicense after reviewing the Autodesk ObjectARX license.'
}

if ($null -eq (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The dotnet CLI is required to verify Autodesk NuGet repository signatures.'
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$destinationRoot = Join-Path $repoRoot 'lib\AutoCAD'
$cacheRoot = Join-Path $repoRoot 'artifacts\autocad-nuget-cache'
$nugetBaseUrl = 'https://api.nuget.org/v3-flatcontainer'

$releases = @(
    [pscustomobject]@{
        Year = '2017'; Series = 'R21.0'; Version = '21.0.2'; LibraryFolder = '45'
        Packages = [ordered]@{
            'AutoCAD.NET' = '1E1C2559FD9B2AA5AC90D5EA0DCFF9FE1AD6C8A19E55709045EF16BE001848F1'
            'AutoCAD.NET.Core' = '01F7D9FC3AB3B905A5C07C69E763C332D2593EB5BFD60678B2B624599D840AB0'
            'AutoCAD.NET.Model' = '2B006516DEA260582296FC0596FDFD3E684A1C02158DF9DF8BD7FFD4639579F9'
        }
        Files = [ordered]@{
            'AcMgd.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET'; Sha256 = '0286D4F351E91BA7E010CE039C553C3E05536381C5D07B9A3372986FA871577C'; RequireSignature = $false }
            'AcWindows.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET'; Sha256 = '0EC4390BCB760D85915BAD5557AC304FDDAE8C73B0695B096A697BE4E137E1E6'; RequireSignature = $false }
            'AdWindows.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET'; Sha256 = '9680D19A36434395870D6CA3738FEAD6153F5DA2D8A762DDEB2AE78B7F99EB96'; RequireSignature = $true }
            'AcCoreMgd.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET.Core'; Sha256 = '6F8FF6F09D189F129C494875CCA9CC2B2AD2690E5C44B983221EE5581446C6E3'; RequireSignature = $false }
            'AcDbMgd.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET.Model'; Sha256 = 'B336193325FDEC247B2A6B6AE736B00D473B8246FCA1483BB5F260527D226035'; RequireSignature = $false }
        }
    },
    [pscustomobject]@{
        Year = '2018'; Series = 'R22.0'; Version = '22.0.0'; LibraryFolder = 'net46'
        Packages = [ordered]@{
            'AutoCAD.NET' = 'E8F7896F7099DA64E32C59AEA35B7C61923849EDC59B7D26F9C99A707A463DA6'
            'AutoCAD.NET.Core' = 'B858C3E99EC796FA8147496D0DC1D13F61A7340279AEC171753E9CA900A190C1'
            'AutoCAD.NET.Model' = 'FA1252A798B0F681E25271AD7CBC582FF940295475788999DE82F8CE151647A8'
        }
        Files = [ordered]@{
            'AcMgd.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET'; Sha256 = '9AC4DF1F61D0A2AB2C95BDD06585D8B6FACCF6EEC3FEDF385000BB6EEC40CDDD'; RequireSignature = $true }
            'AcWindows.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET'; Sha256 = 'F3E3C4D0CBC9DB83A985C82ADCDB214C696F64D105082BDEEC845E3D707017A9'; RequireSignature = $true }
            'AdWindows.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET'; Sha256 = '8DCB04204B6CEA4206F1AD53A1D802951C9C1D7A92126D0CAE48639EEC037E5A'; RequireSignature = $true }
            'AcCoreMgd.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET.Core'; Sha256 = '992D402416660EC66CEFD5661E001091984A399A995ECE97B886C68C023A25B3'; RequireSignature = $true }
            'AcDbMgd.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET.Model'; Sha256 = '84DF8A7C65A82C61A5E85E93AF32BEA62069A8A671C75C7A81679DCCB840AC51'; RequireSignature = $true }
        }
    },
    [pscustomobject]@{
        Year = '2019'; Series = 'R23.0'; Version = '23.0.0'; LibraryFolder = 'net47'
        Packages = [ordered]@{
            'AutoCAD.NET' = 'AF3704E6DC6C077260C75F70CE73BEA1EB4B52CD18DEDE905247F35C1EBAD225'
            'AutoCAD.NET.Core' = '4C0C9E50F7206171D71C96CF919854253A55B4B8F6C10C01320BD7D29B340B33'
            'AutoCAD.NET.Model' = '71E344DB2896851D167780C461C86AE97FE4EFA84C0E30031DAB06F111BC4AF8'
        }
        Files = [ordered]@{
            'AcMgd.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET'; Sha256 = '3C98B3AF7D8BD82924A39C23E15DAA4F7872E7602169AB3361EC887745D94D30'; RequireSignature = $true }
            'AcWindows.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET'; Sha256 = '03D66B89CB0980F9EDCCE1F75274C03C954ADA01BD229BA8606550F2D0AABF88'; RequireSignature = $true }
            'AdWindows.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET'; Sha256 = 'AAEB4164E70EE8E753BFA7492C6DEA9DA3876DC2CB1CE4D2E02CDB99E2D4F58A'; RequireSignature = $true }
            'AcCoreMgd.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET.Core'; Sha256 = '6884F51B2040B1462D20BFB238414ABEEBC155423974561E0B66B38829F6016C'; RequireSignature = $true }
            'AcDbMgd.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET.Model'; Sha256 = '477C68FC5FC5CE175E1E9400247D83E97113332D5D198B2AB497E46E21C24250'; RequireSignature = $true }
        }
    },
    [pscustomobject]@{
        Year = '2020'; Series = 'R23.1'; Version = '23.1.0'; LibraryFolder = 'net47'
        Packages = [ordered]@{
            'AutoCAD.NET' = '6076F685E69B6922B3B9CDEFD28122D9D60FCAB5CBD83C19FB628C6DD5C3C936'
            'AutoCAD.NET.Core' = 'A74A9F80DF20F7EE8867929BB9545E748153C0F662446CC971D453E2A1265D49'
            'AutoCAD.NET.Model' = '30A86DB2EE583F510C2FC6CDA7ABE2B226A834A8D7495D629FDDAC978BD34C75'
        }
        Files = [ordered]@{
            'AcMgd.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET'; Sha256 = 'ACD3FBECF0F6C0712430E236A546344B2BD2A11DA53B6F643C77942C75897A5F'; RequireSignature = $true }
            'AcWindows.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET'; Sha256 = '4241D0B99EC75262FD1E94FDF5DBC64118C41834513B7C6CEDC69BD5FD7704D3'; RequireSignature = $true }
            'AdWindows.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET'; Sha256 = 'A5374B3D69AC945BCB5A2ED243DFB34E321A16799223BD1247FE9CB67A276ED0'; RequireSignature = $true }
            'AcCoreMgd.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET.Core'; Sha256 = 'D1DE39A2A4214C42613AA400BC659E205BD2E59A472EE2B1180F6DCA45FF1B71'; RequireSignature = $true }
            'AcDbMgd.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET.Model'; Sha256 = 'FB6B7384709DDBB612FF933952A27FEA6B1F57DDB4221E041BD55E25CBC00857'; RequireSignature = $true }
        }
    },
    [pscustomobject]@{
        Year = '2021'; Series = 'R24.0'; Version = '24.0.0'; LibraryFolder = 'net47'
        Packages = [ordered]@{
            'AutoCAD.NET' = '8470E433BA8E0AA9F7BAA7B1C1317E64EFB6542C6CA3D8F58C1699A8D3DEA07D'
            'AutoCAD.NET.Core' = 'B9D5EB6FDB3BB61D03D99BE52AF85108F65171439BD91098716618C52703E526'
            'AutoCAD.NET.Model' = '0853021823C038F88CD0055910150FFA951BF4E5ADA810F8469C08E8BFF66408'
        }
        Files = [ordered]@{
            'AcMgd.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET'; Sha256 = '9E33A7A91030F36CA340859F48E25C0F5FA396E5F173D717ABFBD898186A9901'; RequireSignature = $true }
            'AcWindows.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET'; Sha256 = '4D2FF9E254B6DC6AB659AFCF821AE44CE00CC8DD78D9F45C9A7C5B3A83CF38BA'; RequireSignature = $true }
            'AdWindows.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET'; Sha256 = 'E0447A70CF3A7A81CF9BBEE965C6FC2A7B5C27B375E63CC243A6DB358D8DF14E'; RequireSignature = $true }
            'AcCoreMgd.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET.Core'; Sha256 = '68BBE136DC66C936ADAC9648465D79D746C0093141B6A1D41B0D10EE44A8AC65'; RequireSignature = $true }
            'AcDbMgd.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET.Model'; Sha256 = 'E6A6058E89B4AF5D2EB1373E641254529FE57460594B46F02297219AF3C7C527'; RequireSignature = $true }
        }
    },
    [pscustomobject]@{
        Year = '2022'; Series = 'R24.1'; Version = '24.1.51000'; LibraryFolder = 'net47'
        Packages = [ordered]@{
            'AutoCAD.NET' = 'E33E0691EEF277450B65173810D3E8CC72F284FE1DB168E292D6591E0BF59943'
            'AutoCAD.NET.Core' = '59FE3263D3B768A8DF16F81E6FC8D55C36D142FB1F0E79DECBB9D99ACFEAD9BE'
            'AutoCAD.NET.Model' = 'FF297275494B4F197F9A6423ADF638F3D1BB42F80F3A5776720872033A58200D'
        }
        Files = [ordered]@{
            'AcMgd.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET'; Sha256 = 'F3A045A79E511D8C6EF774008B0D5CDD5064CD8AD891F098AE449767D37CD720'; RequireSignature = $true }
            'AcWindows.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET'; Sha256 = 'DC1ED056EECA3DF387499D9A7D6A4AC699CA48DAD7A8260AFDD4E205DF755EAE'; RequireSignature = $true }
            'AdWindows.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET'; Sha256 = 'F4B55342580A62EDBEFA8C24F771E64FEAF400B141D926DA05BF7BD0412C2282'; RequireSignature = $true }
            'AcCoreMgd.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET.Core'; Sha256 = '54AA0F64F3641717FB28D8BE0F79B3522FC9A87C0B64B2F3EF70D8DF7F0CA7B9'; RequireSignature = $true }
            'AcDbMgd.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET.Model'; Sha256 = '1113FF4563FBD1CDBEE298FF00D5453E4425D9E775D0D52D999F22DB2B9CCD87'; RequireSignature = $true }
        }
    },
    [pscustomobject]@{
        Year = '2023'; Series = 'R24.2'; Version = '24.2.0'; LibraryFolder = 'net47'
        Packages = [ordered]@{
            'AutoCAD.NET' = '105802778034EFEF7568CCCF7CC59D4E28F3985921CEEE0364EC07D8B0D4D903'
            'AutoCAD.NET.Core' = '2AB2CA2B820D9F8C7F4A8DBC7C79BC52FC5E17EE74A97B7B4770649BE6311C18'
            'AutoCAD.NET.Model' = '1C39285B024DDFD119D7DD2D06531F936FEFEF630361F6202669470E882E267F'
        }
        Files = [ordered]@{
            'AcMgd.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET'; Sha256 = 'AD7F44ECCF672180BFE998DF4DF2E6E67A9F6269E7F1DEE7E74AD51D59AE6094'; RequireSignature = $true }
            'AcWindows.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET'; Sha256 = '9C3EA5461C552BC8368BC62E9BA2E7895F55343F4A0EBCD64B076EE6A7613AD4'; RequireSignature = $true }
            'AdWindows.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET'; Sha256 = '6DBE919FE7B10BEAE2F7D1307527FE4D5B3A98A903D56F20EAD3BCBCE615B8A9'; RequireSignature = $true }
            'AcCoreMgd.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET.Core'; Sha256 = 'D3FAF7CE1BCA36976410FAFBBFCD743F3FD4EE7E778D58BA1D752D5C444A4BB2'; RequireSignature = $true }
            'AcDbMgd.dll' = [pscustomobject]@{ Package = 'AutoCAD.NET.Model'; Sha256 = '8646A67989C76B6F98E3525632EA2288FCBCC3AF6A4CB66C99A3F9771FAFFAA4'; RequireSignature = $true }
        }
    }
)

function Test-Sha256 {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Expected
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }

    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    return [string]::Equals($actual, $Expected, [StringComparison]::OrdinalIgnoreCase)
}

function Test-AutodeskAssembly {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$ExpectedSha256,
        [Parameter(Mandatory = $true)]
        [bool]$RequireSignature
    )

    if (-not (Test-Sha256 -Path $Path -Expected $ExpectedSha256)) {
        return $false
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -eq [System.Management.Automation.SignatureStatus]::Valid -and
        $null -ne $signature.SignerCertificate -and
        $signature.SignerCertificate.Subject.IndexOf('Autodesk, Inc', [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        return $true
    }

    return -not $RequireSignature -and
        $signature.Status -eq [System.Management.Automation.SignatureStatus]::NotSigned
}

function Copy-ZipEntry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ArchivePath,
        [Parameter(Mandatory = $true)]
        [string]$EntryPath,
        [Parameter(Mandatory = $true)]
        [string]$DestinationPath
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        $entry = $archive.GetEntry($EntryPath)
        if ($null -eq $entry) {
            throw "Package entry is missing: $EntryPath"
        }

        $input = $entry.Open()
        try {
            $output = [System.IO.File]::Create($DestinationPath)
            try {
                $input.CopyTo($output)
            }
            finally {
                $output.Dispose()
            }
        }
        finally {
            $input.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

foreach ($release in $releases) {
    $packageDirectory = Join-Path $cacheRoot $release.Series
    $seriesDirectory = Join-Path $destinationRoot $release.Series
    New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $seriesDirectory -Force | Out-Null

    foreach ($package in $release.Packages.GetEnumerator()) {
        $packageId = $package.Key.ToLowerInvariant()
        $packageVersion = $release.Version.ToLowerInvariant()
        $packageFileName = "$packageId.$packageVersion.nupkg"
        $packagePath = Join-Path $packageDirectory $packageFileName

        if (-not (Test-Sha256 -Path $packagePath -Expected $package.Value)) {
            $downloadPath = "$packagePath.download"
            if (Test-Path -LiteralPath $downloadPath) {
                Remove-Item -LiteralPath $downloadPath -Force
            }

            $packageUrl = "$nugetBaseUrl/$packageId/$packageVersion/$packageFileName"
            Invoke-WebRequest -Uri $packageUrl -OutFile $downloadPath -UseBasicParsing
            if (-not (Test-Sha256 -Path $downloadPath -Expected $package.Value)) {
                Remove-Item -LiteralPath $downloadPath -Force
                throw "NuGet package hash verification failed: $($package.Key) $($release.Version)"
            }

            Move-Item -LiteralPath $downloadPath -Destination $packagePath -Force
        }

        $verifyOutput = @(& dotnet nuget verify $packagePath --all --verbosity minimal 2>&1)
        $verifyOutput | ForEach-Object { Write-Output $_ }
        if ($LASTEXITCODE -ne 0) {
            throw "NuGet repository signature verification failed: $($package.Key) $($release.Version)"
        }

        foreach ($file in $release.Files.GetEnumerator() | Where-Object { $_.Value.Package -eq $package.Key }) {
            $destination = Join-Path $seriesDirectory $file.Key
            $temporaryPath = "$destination.download"
            if (Test-Path -LiteralPath $temporaryPath) {
                Remove-Item -LiteralPath $temporaryPath -Force
            }

            $entryPath = "lib/$($release.LibraryFolder)/$($file.Key)"
            Copy-ZipEntry -ArchivePath $packagePath -EntryPath $entryPath -DestinationPath $temporaryPath
            if (-not (Test-AutodeskAssembly -Path $temporaryPath -ExpectedSha256 $file.Value.Sha256 -RequireSignature $file.Value.RequireSignature)) {
                Remove-Item -LiteralPath $temporaryPath -Force
                throw "Autodesk assembly verification failed: $($release.Series)\$($file.Key)"
            }

            Move-Item -LiteralPath $temporaryPath -Destination $destination -Force
            Write-Output "Installed official Autodesk reference $($release.Series)\$($file.Key)"
        }
    }
}

Write-Output "Official Autodesk AutoCAD 2017-2023 reference assemblies are ready: $destinationRoot"
