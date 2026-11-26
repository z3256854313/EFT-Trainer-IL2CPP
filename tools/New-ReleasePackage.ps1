param(
    [Parameter(Mandatory = $true)]
    [string] $DllPath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $Version,

    [Parameter(Mandatory = $true)]
    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'

$resolvedDll = (Resolve-Path -LiteralPath $DllPath).Path
if ([IO.Path]::GetExtension($resolvedDll) -ne '.dll') {
    throw "DllPath must point to a DLL file."
}

$outputFullPath = [IO.Path]::GetFullPath($OutputPath)
if ([IO.Path]::GetExtension($outputFullPath) -ne '.zip') {
    throw "OutputPath must end with .zip."
}

$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$stage = Join-Path $tempBase ("EFT-Trainer-Package-" + [Guid]::NewGuid().ToString('N'))
$stageFullPath = [IO.Path]::GetFullPath($stage)
if (-not $stageFullPath.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Temporary staging directory is outside the system temporary directory."
}

try {
    New-Item -ItemType Directory -Path $stageFullPath -Force | Out-Null
    Copy-Item -LiteralPath $resolvedDll -Destination (Join-Path $stageFullPath 'TrainerIL2CPP.dll')

    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $stageFullPath 'TrainerIL2CPP.dll')).Hash
    $manifest = [ordered]@{
        version   = $Version
        dll       = 'TrainerIL2CPP.dll'
        dllSha256 = $hash
    }
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stageFullPath 'release.json') -Encoding utf8NoBOM
    "$hash  TrainerIL2CPP.dll" | Set-Content -LiteralPath (Join-Path $stageFullPath 'SHA256.txt') -Encoding utf8NoBOM

    @"
EFT Trainer IL2CPP $Version

Install:
  1. Exit EFT, SPT Server and the launcher.
  2. Copy TrainerIL2CPP.dll to:
     <EFT_ROOT>\BepInEx\plugins\TrainerIL2CPP\TrainerIL2CPP.dll
  3. Start the game through the normal SPT launcher.
  4. Press Right Alt to open the panel.

SHA-256:
  $hash  TrainerIL2CPP.dll

Use this package only with a matching, self-hosted offline SPT / EFT installation.
"@ | Set-Content -LiteralPath (Join-Path $stageFullPath 'README.txt') -Encoding utf8NoBOM

    $outputDirectory = Split-Path -Parent $outputFullPath
    if ($outputDirectory) {
        New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    }
    if (Test-Path -LiteralPath $outputFullPath) {
        Remove-Item -LiteralPath $outputFullPath -Force
    }

    Compress-Archive -Path (Join-Path $stageFullPath '*') -DestinationPath $outputFullPath -CompressionLevel Optimal
    Write-Output $outputFullPath
}
finally {
    if (Test-Path -LiteralPath $stageFullPath) {
        Remove-Item -LiteralPath $stageFullPath -Recurse -Force
    }
}
