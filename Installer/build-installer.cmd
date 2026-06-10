@echo off
setlocal enabledelayedexpansion

REM ============================================================
REM Snap.Hutao.Remastered — Installer Build Script
REM
REM Builds the project, signs binaries, compiles the Inno Setup
REM installer, and signs the final installer executable.
REM
REM Environment variables (used by CI):
REM   CERTIFICATE      — Base64-encoded PFX certificate (optional)
REM   PW               — Certificate password (optional)
REM   SIGN_HASH        — Signing hash algorithm (default: SHA256)
REM   APP_VERSION      — Application version (default: from .iss)
REM
REM Prerequisites:
REM   - .NET SDK 10.0
REM   - Inno Setup (iscc.exe) in PATH
REM   - Windows SDK (signtool.exe) in PATH
REM ============================================================

set SCRIPT_DIR=%~dp0
set REPO_ROOT=%SCRIPT_DIR%..
set PUBLISH_DIR=%SCRIPT_DIR%Publish
set PROJECT_FILE=%REPO_ROOT%\src\Snap.Hutao.Remastered\Snap.Hutao.Remastered\Snap.Hutao.Remastered.csproj
set SIGN_HASH=%SIGN_HASH:SHA256=SHA256%
if "%SIGN_HASH%"=="" set SIGN_HASH=SHA256

set CERT_FILE=%TEMP%\SnapHutaoBuildCert.pfx

echo ============================================================
echo Snap.Hutao.Remastered — Installer Build
echo ============================================================
echo REPO_ROOT:    %REPO_ROOT%
echo PUBLISH_DIR:  %PUBLISH_DIR%
echo PROJECT:      %PROJECT_FILE%
echo.

REM ------------------------------------------------------------
REM Step 1: Clean previous publish output
REM ------------------------------------------------------------
echo [Step 1/7] Cleaning publish directory...
if exist "%PUBLISH_DIR%" (
    rmdir /s /q "%PUBLISH_DIR%" 2>nul
)
mkdir "%PUBLISH_DIR%"
echo Done.
echo.

REM ------------------------------------------------------------
REM Step 2: Restore and publish the project
REM ------------------------------------------------------------
echo [Step 2/7] Restoring and publishing project...
echo.

dotnet restore "%PROJECT_FILE%"
if %ERRORLEVEL% neq 0 (
    echo ERROR: dotnet restore failed.
    exit /b 1
)

dotnet publish "%PROJECT_FILE%" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:WindowsAppSDKSelfContained=true ^
    -o "%PUBLISH_DIR%"
if %ERRORLEVEL% neq 0 (
    echo ERROR: dotnet publish failed.
    exit /b 1
)
echo.
echo Publish completed successfully.
echo.

REM ------------------------------------------------------------
REM Step 3: Sign all executables and DLLs in the publish output
REM ------------------------------------------------------------
if not "!CERTIFICATE!"=="" (
    echo [Step 3/7] Signing published binaries...
    echo.

    REM Decode base64 certificate to temp file
    > "%CERT_FILE%.b64" echo(!CERTIFICATE!
    certutil -decode "%CERT_FILE%.b64" "%CERT_FILE%" >nul 2>&1
    del "%CERT_FILE%.b64" 2>nul

    if not exist "%CERT_FILE%" (
        echo WARNING: Failed to decode certificate, skipping signing.
        echo.
    ) else (
        REM Find signtool
        set SIGNTOOL=
        for /f "tokens=*" %%i in ('where signtool 2^>nul') do (
            set SIGNTOOL=%%i
        )

        if "!SIGNTOOL!"=="" (
            REM Fallback to common SDK paths
            for /f "tokens=*" %%i in ('dir /s /b "%ProgramFiles(x86)%\Windows Kits\10\bin\*\x64\signtool.exe" 2^>nul') do (
                if not defined SIGNTOOL set SIGNTOOL=%%i
            )
        )

        if "!SIGNTOOL!"=="" (
            echo WARNING: signtool.exe not found, skipping signing.
            echo.
        ) else (
            echo Using signtool: !SIGNTOOL!
            echo.

            REM Sign all .exe and .dll files
            for /r "%PUBLISH_DIR%" %%f in (*.exe *.dll) do (
                "!SIGNTOOL!" sign /fd %SIGN_HASH% /a /f "%CERT_FILE%" /p "%PW%" /tr http://timestamp.digicert.com /td %SIGN_HASH% "%%f"
                if !ERRORLEVEL! neq 0 (
                    echo WARNING: Failed to sign %%f
                ) else (
                    echo Signed: %%~nxf
                )
            )
            echo.
        )

        del "%CERT_FILE%" 2>nul
        echo Binary signing completed.
    )
) else (
    echo [Step 3/7] Skipping binary signing (CERTIFICATE not set).
)
echo.

REM ------------------------------------------------------------
REM Step 4: Download VC++ redistributable if not present
REM ------------------------------------------------------------
echo [Step 4/7] Checking VC++ redistributable...
echo.
if not exist "%SCRIPT_DIR%VC_redist.x64.exe" (
    echo Downloading VC_redist.x64.exe...
    curl -sL -o "%SCRIPT_DIR%VC_redist.x64.exe" "https://aka.ms/vs/17/release/vc_redist.x64.exe"
    if !ERRORLEVEL! neq 0 (
        echo WARNING: Failed to download VC_redist.x64.exe
        echo The installer may not include the VC++ runtime.
    ) else (
        echo VC_redist.x64.exe downloaded successfully.
    )
) else (
    echo VC_redist.x64.exe already present, skipping download.
)
echo.

REM ------------------------------------------------------------
REM Step 5: Determine output version
REM ------------------------------------------------------------
if not "%APP_VERSION%"=="" (
    set VERSION=%APP_VERSION%
) else (
    REM Extract version from .iss file
    for /f "tokens=2 delims==" %%i in ('findstr "MyAppVersion" "%SCRIPT_DIR%installer.iss"') do (
        set VERSION=%%i
        REM Remove quotes and whitespace
        set VERSION=!VERSION:"=!
        set VERSION=!VERSION: =!
    )
)
echo Version: %VERSION%
echo.

REM ------------------------------------------------------------
REM Step 6: Compile the Inno Setup installer
REM ------------------------------------------------------------
echo [Step 6/7] Compiling installer with Inno Setup...
echo.

set ISCC=
for /f "tokens=*" %%i in ('where iscc 2^>nul') do (
    set ISCC=%%i
)
if "%ISCC%"=="" (
    for /f "tokens=*" %%i in ('dir /s /b "%ProgramFiles(x86)%\Inno Setup*\iscc.exe" 2^>nul') do (
        if not defined ISCC set ISCC=%%i
    )
)

if "%ISCC%"=="" (
    echo ERROR: Inno Setup (iscc.exe) not found.
    echo Install from: https://jrsoftware.org/isinfo.php
    exit /b 1
)

echo Using iscc: %ISCC%

REM Change to repo root so the relative paths in .iss resolve correctly
pushd "%REPO_ROOT%"
"%ISCC%" "%SCRIPT_DIR%installer.iss"
set ISCC_RESULT=%ERRORLEVEL%
popd

if %ISCC_RESULT% neq 0 (
    echo ERROR: Inno Setup compilation failed.
    exit /b 1
)

REM Find the generated installer
set INSTALLER_DIR=%REPO_ROOT%\publish
echo Installer output directory: %INSTALLER_DIR%
dir "%INSTALLER_DIR%\Snap.Hutao.Remastered-*.exe" /b 2>nul

echo Installer compiled successfully.
echo.

REM ------------------------------------------------------------
REM Step 7: Sign the installer
REM ------------------------------------------------------------
if not "!CERTIFICATE!"=="" (
    echo [Step 7/7] Signing installer...
    echo.

    REM Re-decode certificate for installer signing
    > "%CERT_FILE%.b64" echo(!CERTIFICATE!
    certutil -decode "%CERT_FILE%.b64" "%CERT_FILE%" >nul 2>&1
    del "%CERT_FILE%.b64" 2>nul

    if exist "%CERT_FILE%" (
        set SIGNTOOL=
        for /f "tokens=*" %%i in ('where signtool 2^>nul') do set SIGNTOOL=%%i
        if "!SIGNTOOL!"=="" (
            for /f "tokens=*" %%i in ('dir /s /b "%ProgramFiles(x86)%\Windows Kits\10\bin\*\x64\signtool.exe" 2^>nul') do (
                if not defined SIGNTOOL set SIGNTOOL=%%i
            )
        )

        if not "!SIGNTOOL!"=="" (
            for /f "tokens=*" %%i in ('dir "%INSTALLER_DIR%\Snap.Hutao.Remastered-*.exe" /b 2^>nul') do (
                "!SIGNTOOL!" sign /fd %SIGN_HASH% /a /f "%CERT_FILE%" /p "%PW%" /tr http://timestamp.digicert.com /td %SIGN_HASH% "%INSTALLER_DIR%\%%i"
                if !ERRORLEVEL! equ 0 (
                    echo Signed installer: %%i
                ) else (
                    echo WARNING: Failed to sign installer %%i
                )
            )
        )

        del "%CERT_FILE%" 2>nul
    )
    echo.
    echo Installer signing completed.
) else (
    echo [Step 7/7] Skipping installer signing (CERTIFICATE not set).
)
echo.

REM ------------------------------------------------------------
REM Done
REM ------------------------------------------------------------
echo ============================================================
echo Build completed successfully!
echo Installer location: %INSTALLER_DIR%
dir "%INSTALLER_DIR%\Snap.Hutao.Remastered-*.exe" /b 2>nul
echo ============================================================

endlocal
