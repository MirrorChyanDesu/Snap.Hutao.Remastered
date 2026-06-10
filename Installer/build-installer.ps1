#Requires -Version 7.0

<#
.SYNOPSIS
    Snap.Hutao.Remastered - Installer Build Script
.DESCRIPTION
    Builds the project, signs binaries, compiles the Inno Setup installer,
    and signs the final installer executable.
.PARAMETER Certificate
    Base64-encoded PFX certificate for code signing.
.PARAMETER Password
    Certificate password.
.PARAMETER SignHash
    Signing hash algorithm (default: SHA256).
.PARAMETER AppVersion
    Application version (default: extracted from installer.iss).
#>

param(
    [string]$Certificate = $env:CERTIFICATE,
    [string]$Password = $env:PW,
    [string]$SignHash = $env:SIGN_HASH,
    [string]$AppVersion = $env:APP_VERSION
)

$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

# Paths
$ScriptDir = Split-Path -Parent $PSCommandPath
$RepoRoot = Resolve-Path (Join-Path $ScriptDir '..')
$PublishDir = Join-Path $ScriptDir 'Publish'
$ProjectFile = Join-Path $RepoRoot 'src\Snap.Hutao.Remastered\Snap.Hutao.Remastered\Snap.Hutao.Remastered.csproj'
$IssFile = Join-Path $ScriptDir 'installer.iss'
$VcRedistPath = Join-Path $ScriptDir 'VC_redist.x64.exe'
$InstallerDir = Join-Path $RepoRoot 'publish'

if (-not $SignHash) { $SignHash = 'SHA256' }

Write-Host "============================================================"
Write-Host "Snap.Hutao.Remastered - Installer Build"
Write-Host "============================================================"
Write-Host "Repo root:    $RepoRoot"
Write-Host "Publish dir:  $PublishDir"
Write-Host "Project:      $ProjectFile"
Write-Host ""

# -----------------------------------------------------------
# Step 1: Clean previous publish output
# -----------------------------------------------------------
Write-Host "[Step 1/7] Cleaning publish directory..."
if (Test-Path $PublishDir) {
    Remove-Item -Recurse -Force $PublishDir
}
New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null
Write-Host "Done."
Write-Host ""

# -----------------------------------------------------------
# Step 2: Restore and build the project (use dotnet build like Cake)
# -----------------------------------------------------------
Write-Host "[Step 2/7] Restoring and building project..."
Write-Host ""

Write-Host "dotnet restore..."
& dotnet restore $ProjectFile
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet restore failed."
    exit 1
}

$BuildDir = Join-Path $RepoRoot 'src\Snap.Hutao.Remastered\Snap.Hutao.Remastered\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64'

Write-Host "dotnet build..."
& dotnet build $ProjectFile `
    -c Release `
    --self-contained true `
    -p:Platform=x64 `
    -p:WindowsAppSDKSelfContained=true `
    -p:AppxPackageSigningEnabled=false `
    -p:AppxBundle=Never
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet build failed."
    exit 1
}

Write-Host ""
Write-Host "Copying build output to publish directory..."
if (Test-Path $BuildDir) {
    # Copy everything from build output (which is known to work) to Publish dir
    Copy-Item -Path "$BuildDir\*" -Destination $PublishDir -Recurse -Force
} else {
    Write-Error "Build output directory not found: $BuildDir"
    exit 1
}
Write-Host ""
Write-Host "Build and copy completed successfully."
Write-Host ""

# -----------------------------------------------------------
# Step 3: Sign all executables and DLLs in the publish output
# -----------------------------------------------------------
if ($Certificate) {
    Write-Host "[Step 3/7] Signing published binaries..."
    Write-Host ""

    $CertFile = Join-Path $env:TEMP 'SnapHutaoBuildCert.pfx'
    $CertB64File = "$CertFile.b64"

    try {
        # Decode base64 certificate to temp file
        [System.IO.File]::WriteAllText($CertB64File, $Certificate)
        & certutil -decode $CertB64File $CertFile | Out-Null
        Remove-Item $CertB64File -Force -ErrorAction SilentlyContinue

        if (-not (Test-Path $CertFile)) {
            Write-Warning "Failed to decode certificate, skipping signing."
        } else {
            # Find signtool
            $SignTool = Get-Command 'signtool.exe' -ErrorAction SilentlyContinue
            if (-not $SignTool) {
                # Fallback to common SDK paths
                $SignToolPath = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -Recurse -ErrorAction SilentlyContinue |
                    Select-Object -First 1 -ExpandProperty FullName
                if ($SignToolPath) {
                    $SignTool = $SignToolPath
                }
            }

            if (-not $SignTool) {
                Write-Warning "signtool.exe not found, skipping signing."
            } else {
                $SignToolExe = if ($SignTool -is [System.Management.Automation.CommandInfo]) { $SignTool.Source } else { $SignTool }
                Write-Host "Using signtool: $SignToolExe"
                Write-Host ""

                # Sign all .exe and .dll files
                Get-ChildItem $PublishDir -Recurse -Include *.exe, *.dll | ForEach-Object {
                    $file = $_.FullName
                    Write-Host "Signing: $($_.Name)..."
                    & $SignToolExe sign /fd $SignHash /a /f $CertFile /p $Password /tr http://timestamp.digicert.com /td $SignHash $file
                    if ($LASTEXITCODE -ne 0) {
                        Write-Warning "Failed to sign $($_.Name)"
                    } else {
                        Write-Host "Signed: $($_.Name)"
                    }
                }
                Write-Host ""
            }

            Remove-Item $CertFile -Force -ErrorAction SilentlyContinue
            Write-Host "Binary signing completed."
        }
    } catch {
        Write-Warning "Signing step failed: $_"
        # Clean up temp files
        Remove-Item $CertFile -Force -ErrorAction SilentlyContinue
        Remove-Item $CertB64File -Force -ErrorAction SilentlyContinue
    }
} else {
    Write-Host "[Step 3/7] Skipping binary signing (CERTIFICATE not set)."
}
Write-Host ""

# -----------------------------------------------------------
# Step 4: Download VC++ redistributable if not present
# -----------------------------------------------------------
Write-Host "[Step 4/7] Checking VC++ redistributable..."
Write-Host ""
if (-not (Test-Path $VcRedistPath)) {
    Write-Host "Downloading VC_redist.x64.exe..."
    try {
        Invoke-WebRequest -Uri 'https://aka.ms/vs/17/release/vc_redist.x64.exe' -OutFile $VcRedistPath -UseBasicParsing
        Write-Host "VC_redist.x64.exe downloaded successfully."
    } catch {
        Write-Warning "Failed to download VC_redist.x64.exe. The installer may not include the VC++ runtime."
    }
} else {
    Write-Host "VC_redist.x64.exe already present, skipping download."
}
Write-Host ""

# -----------------------------------------------------------
# Step 5: Determine output version
# -----------------------------------------------------------
if ($AppVersion) {
    $Version = $AppVersion
} else {
    # Extract version from .iss file
    $issContent = Get-Content $IssFile -Raw
    if ($issContent -match '#define\s+MyAppVersion\s+"([^"]+)"') {
        $Version = $Matches[1]
    } else {
        Write-Warning "Could not extract version from installer.iss"
        $Version = '0.0.0.0'
    }
}
Write-Host "Version: $Version"
Write-Host ""

# -----------------------------------------------------------
# Step 6: Compile the Inno Setup installer
# -----------------------------------------------------------
Write-Host "[Step 6/7] Compiling installer with Inno Setup..."
Write-Host ""

$Iscc = Get-Command 'iscc.exe' -ErrorAction SilentlyContinue
if (-not $Iscc) {
    $IsccPath = Get-ChildItem "${env:ProgramFiles(x86)}\Inno Setup*\iscc.exe" -Recurse -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
    if ($IsccPath) {
        $Iscc = $IsccPath
    }
}

if (-not $Iscc) {
    Write-Error "Inno Setup (iscc.exe) not found. Install from: https://jrsoftware.org/isinfo.php"
    exit 1
}

$IsccExe = if ($Iscc -is [System.Management.Automation.CommandInfo]) { $Iscc.Source } else { $Iscc }
Write-Host "Using iscc: $IsccExe"

# Change to repo root so the relative paths in .iss resolve correctly
Push-Location $RepoRoot
try {
    & $IsccExe $IssFile
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Inno Setup compilation failed."
        exit 1
    }
} finally {
    Pop-Location
}

Write-Host "Installer output directory: $InstallerDir"
Get-ChildItem (Join-Path $InstallerDir 'Snap.Hutao.Remastered-*.exe') -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "  $($_.Name)"
}
Write-Host "Installer compiled successfully."
Write-Host ""

# -----------------------------------------------------------
# Step 7: Sign the installer
# -----------------------------------------------------------
if ($Certificate) {
    Write-Host "[Step 7/7] Signing installer..."
    Write-Host ""

    $CertFile = Join-Path $env:TEMP 'SnapHutaoBuildCert.pfx'
    $CertB64File = "$CertFile.b64"

    try {
        # Re-decode certificate for installer signing
        [System.IO.File]::WriteAllText($CertB64File, $Certificate)
        & certutil -decode $CertB64File $CertFile | Out-Null
        Remove-Item $CertB64File -Force -ErrorAction SilentlyContinue

        if (Test-Path $CertFile) {
            # Find signtool
            $SignTool = Get-Command 'signtool.exe' -ErrorAction SilentlyContinue
            if (-not $SignTool) {
                $SignToolPath = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -Recurse -ErrorAction SilentlyContinue |
                    Select-Object -First 1 -ExpandProperty FullName
                if ($SignToolPath) {
                    $SignTool = $SignToolPath
                }
            }

            if ($SignTool) {
                $SignToolExe = if ($SignTool -is [System.Management.Automation.CommandInfo]) { $SignTool.Source } else { $SignTool }
                Get-ChildItem (Join-Path $InstallerDir 'Snap.Hutao.Remastered-*.exe') -ErrorAction SilentlyContinue | ForEach-Object {
                    $file = $_.FullName
                    Write-Host "Signing installer: $($_.Name)..."
                    & $SignToolExe sign /fd $SignHash /a /f $CertFile /p $Password /tr http://timestamp.digicert.com /td $SignHash $file
                    if ($LASTEXITCODE -eq 0) {
                        Write-Host "Signed installer: $($_.Name)"
                    } else {
                        Write-Warning "Failed to sign installer $($_.Name)"
                    }
                }
            }

            Remove-Item $CertFile -Force -ErrorAction SilentlyContinue
        }
        Write-Host ""
        Write-Host "Installer signing completed."
    } catch {
        Write-Warning "Installer signing failed: $_"
        Remove-Item $CertFile -Force -ErrorAction SilentlyContinue
        Remove-Item $CertB64File -Force -ErrorAction SilentlyContinue
    }
} else {
    Write-Host "[Step 7/7] Skipping installer signing (CERTIFICATE not set)."
}
Write-Host ""

# -----------------------------------------------------------
# Done
# -----------------------------------------------------------
Write-Host "============================================================"
Write-Host "Build completed successfully!"
Write-Host "Installer location: $InstallerDir"
Get-ChildItem (Join-Path $InstallerDir 'Snap.Hutao.Remastered-*.exe') -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "  $($_.Name)"
}
Write-Host "============================================================"
