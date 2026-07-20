[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$installerScriptPath = Join-Path $repoRoot 'Installer\installer.iss'
$installerScript = Get-Content -Raw $installerScriptPath
$expectedThumbprint = '414B476BD7F21B4E8DF2665B1F7DA12F564DB9DD'

function Assert-Policy {
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

Assert-Policy `
    -Condition (-not [regex]::IsMatch($installerScript, '(?im)-addstore\s+Root\b')) `
    -Message 'The installer must never add a certificate to the Root store.'
Assert-Policy `
    -Condition (-not [regex]::IsMatch($installerScript, '(?im)^\s*Source:\s+"[^"]*RootCA\.cer"')) `
    -Message 'The installer must not package the project root CA.'
Assert-Policy `
    -Condition ($installerScript.Contains("#define CodeSigningCertificateThumbprint `"$expectedThumbprint`"")) `
    -Message 'The installer must pin the expected code-signing leaf certificate thumbprint.'
Assert-Policy `
    -Condition ([regex]::IsMatch($installerScript, '(?im)-addstore\s+TrustedPeople\b')) `
    -Message 'The installer must add only the code-signing leaf to TrustedPeople.'
Assert-Policy `
    -Condition (-not [regex]::IsMatch($installerScript, '(?im)-addstore\s+TrustedPublisher\b')) `
    -Message 'TrustedPublisher alone does not establish leaf-only Authenticode chain trust.'
Assert-Policy `
    -Condition ([regex]::IsMatch($installerScript, '(?im)-delstore\s+TrustedPeople\b')) `
    -Message 'The uninstaller must remove installer-owned leaf trust.'
Assert-Policy `
    -Condition ($installerScript.Contains('InstallerCreatedTrustedPeopleThumbprint')) `
    -Message 'The uninstaller must use an installer-ownership marker before removing trust.'
Assert-Policy `
    -Condition ($installerScript.Contains('CertificateExistsInTrustedPeople')) `
    -Message 'The installer must preserve pre-existing leaf trust.'
Assert-Policy `
    -Condition (-not [regex]::IsMatch($installerScript, "(?is)certutil\.exe.{0,160}'-store\s+TrustedPeople")) `
    -Message 'Leaf certificate existence must not rely on certutil -store exit codes.'
Assert-Policy `
    -Condition ($installerScript.Contains('SystemCertificates\TrustedPeople\Certificates')) `
    -Message 'Leaf certificate existence must check the exact LocalMachine certificate-store key.'
Assert-Policy `
    -Condition ([regex]::IsMatch($installerScript, '(?s)if \(CurUninstallStep = usPostUninstall\) and not UninstallSilent then.{0,300}MsgBox')) `
    -Message 'Silent uninstall must not block on the interactive user-data removal prompt.'

foreach ($buildScriptName in 'build-installer.cake', 'publish.cake') {
    $buildScriptPath = Join-Path $repoRoot $buildScriptName
    $buildScript = Get-Content -Raw $buildScriptPath

    Assert-Policy `
        -Condition ($buildScript.Contains('Task("Export code signing certificate")')) `
        -Message "$buildScriptName must export the signing leaf certificate before compiling the installer."
    Assert-Policy `
        -Condition ($buildScript.Contains($expectedThumbprint)) `
        -Message "$buildScriptName must reject an unexpected signing certificate."
    Assert-Policy `
        -Condition ($buildScript.Contains('CodeSigningCertificatePath')) `
        -Message "$buildScriptName must pass the generated leaf certificate to Inno Setup."
}

$installerBuildScript = Get-Content -Raw (Join-Path $repoRoot 'build-installer.cake')
Assert-Policy `
    -Condition ($installerBuildScript.Contains('GitHubActions.Environment.PullRequest.IsPullRequest')) `
    -Message 'Fork pull-request builds must be detected before reading unavailable signing secrets.'
Assert-Policy `
    -Condition ([regex]::IsMatch($installerBuildScript, '(?s)if \(GitHubActions\.IsRunningOnGitHubActions && !isPullRequest\).{0,900}HasEnvironmentVariable\("CERTIFICATE"\)')) `
    -Message 'Installer signing secrets must only be required for non-PR GitHub builds.'
Assert-Policy `
    -Condition ([regex]::IsMatch($installerBuildScript, '(?s)if \(!GitHubActions\.IsRunningOnGitHubActions \|\| isPullRequest\).{0,300}Skip code-signing certificate export')) `
    -Message 'Fork pull-request builds must compile without exporting or installing leaf trust.'

$lifecycleTestScript = Get-Content -Raw (Join-Path $repoRoot 'Installer\tests\Test-InstallerCertificateLifecycle.ps1')
Assert-Policy `
    -Condition ($lifecycleTestScript.Contains('[switch]$SkipInstallerSignatureCheck')) `
    -Message 'Disposable integration tests must explicitly opt out of installer signature verification.'
Assert-Policy `
    -Condition ([regex]::IsMatch($lifecycleTestScript, '(?s)if \(-not \$SkipInstallerSignatureCheck\).{0,500}Get-AuthenticodeSignature')) `
    -Message 'Installer signature verification must remain the default lifecycle-test behavior.'

Assert-Policy `
    -Condition (-not (Test-Path (Join-Path $repoRoot 'SnapHutaoRemasteringProjectRootCA.cer'))) `
    -Message 'The legacy project root CA must not remain as a tracked distribution artifact.'

foreach ($workflowName in 'alpha.yml', 'canary.yml') {
    $workflow = Get-Content -Raw (Join-Path $repoRoot ".github\workflows\$workflowName")
    Assert-Policy `
        -Condition (-not [regex]::IsMatch($workflow, '(?i)Trusted Root|RootCA\.cer')) `
        -Message "$workflowName must not tell users to import the project CA into Trusted Root."
    Assert-Policy `
        -Condition ($workflow.Contains('CodeSigningCertificate')) `
        -Message "$workflowName must publish the code-signing leaf for managed MSIX deployment."
}

Write-Host 'Installer certificate policy checks passed.'
