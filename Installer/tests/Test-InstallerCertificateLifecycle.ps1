[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path $_ -PathType Leaf })]
    [string]$InstallerPath,

    [string]$InstallDirectory = (Join-Path $env:ProgramFiles 'SnapHutaoRemastered-TrustLifecycleTest'),

    [switch]$VerifyPreexistingLeafTrust,

    [string]$CodeSigningCertificatePath,

    [switch]$SkipInstallerSignatureCheck
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$expectedThumbprint = '414B476BD7F21B4E8DF2665B1F7DA12F564DB9DD'
$uninstallRegistryPath = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall'
$testCertificatePath = Join-Path $env:TEMP 'SnapHutaoRemastered-CodeSigning-Test.cer'
$testAddedLeafCertificate = $false
$uninstallerPath = $null
$leafCertificate = $null
$disposeLeafCertificate = $false

function Assert-Condition {
    param(
        [Parameter(Mandatory)]
        [bool]$Condition,

        [Parameter(Mandatory)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-LocalMachineCertificateThumbprints {
    param(
        [Parameter(Mandatory)]
        [string]$StoreName
    )

    $store = [System.Security.Cryptography.X509Certificates.X509Store]::new(
        $StoreName,
        [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
    try {
        $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)
        return @($store.Certificates | ForEach-Object Thumbprint | Sort-Object -Unique)
    }
    finally {
        $store.Dispose()
    }
}

function Assert-StoreUnchanged {
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [string[]]$Expected,

        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [string[]]$Actual,

        [Parameter(Mandatory)]
        [string]$StoreName
    )

    $difference = Compare-Object -ReferenceObject @($Expected) -DifferenceObject @($Actual)
    Assert-Condition -Condition ($null -eq $difference) -Message "$StoreName changed unexpectedly: $($difference | Out-String)"
}

function Invoke-CheckedProcess {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$ArgumentList
    )

    $process = Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -PassThru -Wait
    Assert-Condition -Condition ($process.ExitCode -eq 0) -Message "$FilePath failed with exit code $($process.ExitCode)."
}

function Get-InstalledApplicationEntry {
    foreach ($key in Get-ChildItem $uninstallRegistryPath) {
        $properties = Get-ItemProperty $key.PSPath -ErrorAction SilentlyContinue
        if ($null -ne $properties -and
            $null -ne $properties.PSObject.Properties['DisplayName'] -and
            $properties.DisplayName -eq 'Snap.Hutao.Remastered') {
            return $properties
        }
    }

    return $null
}

function Get-InstallerUninstallerPath {
    $entry = Get-InstalledApplicationEntry
    Assert-Condition -Condition ($null -ne $entry) -Message 'The installer did not create an uninstall entry.'

    $match = [regex]::Match($entry.UninstallString, '^(?:"(?<path>[^"]+)"|(?<path>\S+))')
    Assert-Condition -Condition $match.Success -Message "Cannot parse the uninstall command: $($entry.UninstallString)"
    return $match.Groups['path'].Value
}

$principal = [Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
Assert-Condition -Condition $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) -Message 'Run this integration test in an elevated, disposable Windows environment.'
Assert-Condition -Condition ($null -eq (Get-InstalledApplicationEntry)) -Message 'The disposable test environment must not already have Snap.Hutao.Remastered installed.'

if (-not $SkipInstallerSignatureCheck) {
    $signature = Get-AuthenticodeSignature -FilePath $InstallerPath
    Assert-Condition -Condition ($null -ne $signature.SignerCertificate) -Message 'The installer does not have an Authenticode signer certificate.'
    Assert-Condition -Condition ($signature.SignerCertificate.Thumbprint -eq $expectedThumbprint) -Message 'The installer is not signed by the expected code-signing leaf certificate.'
    $leafCertificate = $signature.SignerCertificate
}
elseif ($CodeSigningCertificatePath) {
    Assert-Condition -Condition (Test-Path $CodeSigningCertificatePath -PathType Leaf) -Message 'The supplied code-signing certificate does not exist.'
    $leafCertificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($CodeSigningCertificatePath)
    $disposeLeafCertificate = $true
    Assert-Condition -Condition ($leafCertificate.Thumbprint -eq $expectedThumbprint) -Message 'The supplied certificate does not match the expected code-signing leaf thumbprint.'
}

if ($VerifyPreexistingLeafTrust) {
    Assert-Condition -Condition ($null -ne $leafCertificate) -Message 'Pre-existing leaf trust verification requires a signed installer or -CodeSigningCertificatePath.'
}

$rootBefore = @(Get-LocalMachineCertificateThumbprints -StoreName Root)
$trustedPeopleBefore = @(Get-LocalMachineCertificateThumbprints -StoreName TrustedPeople)

try {
    if ($VerifyPreexistingLeafTrust -and $expectedThumbprint -notin $trustedPeopleBefore) {
        [System.IO.File]::WriteAllBytes(
            $testCertificatePath,
            $leafCertificate.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert))
        Invoke-CheckedProcess -FilePath 'certutil.exe' -ArgumentList @('-addstore', 'TrustedPeople', $testCertificatePath)
        $testAddedLeafCertificate = $true
        $trustedPeopleBefore = @(Get-LocalMachineCertificateThumbprints -StoreName TrustedPeople)
    }

    $installArguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/DIR=`"$InstallDirectory`"")
    Invoke-CheckedProcess -FilePath $InstallerPath -ArgumentList $installArguments
    Assert-StoreUnchanged -Expected $rootBefore -Actual @(Get-LocalMachineCertificateThumbprints -StoreName Root) -StoreName 'LocalMachine\\Root'

    $trustedPeopleAfterInstall = @(Get-LocalMachineCertificateThumbprints -StoreName TrustedPeople)
    if ($expectedThumbprint -in $trustedPeopleBefore) {
        Assert-StoreUnchanged -Expected $trustedPeopleBefore -Actual $trustedPeopleAfterInstall -StoreName 'LocalMachine\\TrustedPeople after a pre-existing trust entry'
    }
    else {
        Assert-Condition -Condition ($expectedThumbprint -in $trustedPeopleAfterInstall) -Message 'The installer did not add its code-signing leaf to TrustedPeople.'
    }

    Invoke-CheckedProcess -FilePath $InstallerPath -ArgumentList $installArguments
    Assert-StoreUnchanged -Expected $rootBefore -Actual @(Get-LocalMachineCertificateThumbprints -StoreName Root) -StoreName 'LocalMachine\\Root after reinstall'
    $uninstallerPath = Get-InstallerUninstallerPath
    Invoke-CheckedProcess -FilePath $uninstallerPath -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')

    Assert-StoreUnchanged -Expected $rootBefore -Actual @(Get-LocalMachineCertificateThumbprints -StoreName Root) -StoreName 'LocalMachine\\Root after uninstall'
    Assert-StoreUnchanged -Expected $trustedPeopleBefore -Actual @(Get-LocalMachineCertificateThumbprints -StoreName TrustedPeople) -StoreName 'LocalMachine\\TrustedPeople after uninstall'
    Write-Host 'Installer certificate lifecycle checks passed.'
}
finally {
    if ($testAddedLeafCertificate) {
        & certutil.exe -delstore TrustedPeople $expectedThumbprint | Out-Null
    }

    Remove-Item -LiteralPath $testCertificatePath -Force -ErrorAction SilentlyContinue

    if ($disposeLeafCertificate) {
        $leafCertificate.Dispose()
    }
}
