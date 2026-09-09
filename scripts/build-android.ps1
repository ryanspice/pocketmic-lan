param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',
    [switch] $SkipClean,
    [switch] $SkipChecks
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# A stray quote character in the user PATH is inherited by every tool started from here and
# breaks Java worker-process classpath parsing, which kills the Gradle unit-test workers with
# "Could not find or load main class VS". Strip it for this process only.
$env:Path = ($env:Path -replace '"', '')

$Root = Split-Path -Parent $PSScriptRoot
$AndroidRoot = Join-Path $Root 'android'
# The Gradle wrapper is the single source of truth for the build's Gradle version. This script
# used to download and checksum-pin its own copy, which meant the repo declared two different
# versions once the wrapper was added - the classic way a build becomes reproducible for one
# person and not another.
$GradleBat = Join-Path $AndroidRoot 'gradlew.bat'
$AppVersion = '0.1.4'

function Resolve-AndroidSdk {
    $candidates = @(
        $env:ANDROID_SDK_ROOT,
        $env:ANDROID_HOME,
        (Join-Path $env:LOCALAPPDATA 'Android\Sdk')
    ) | Where-Object { $_ -and (Test-Path $_) }

    $sdk = $candidates | Select-Object -First 1
    if (-not $sdk) {
        throw 'Android SDK not found. Install Android Studio + SDK 36, or set ANDROID_SDK_ROOT.'
    }

    $env:ANDROID_SDK_ROOT = $sdk
    $env:ANDROID_HOME = $sdk
    return $sdk
}

function Resolve-JavaHome {
    if ($env:JAVA_HOME -and (Test-Path (Join-Path $env:JAVA_HOME 'bin\java.exe'))) {
        return $env:JAVA_HOME
    }

    $studioJbr = Join-Path $env:ProgramFiles 'Android\Android Studio\jbr'
    if (Test-Path (Join-Path $studioJbr 'bin\java.exe')) {
        $env:JAVA_HOME = $studioJbr
        return $studioJbr
    }

    $java = Get-Command java.exe -ErrorAction SilentlyContinue
    if ($java) {
        $resolved = Split-Path -Parent (Split-Path -Parent $java.Source)
        $env:JAVA_HOME = $resolved
        return $resolved
    }

    throw 'JDK 17+ not found. Install Android Studio or set JAVA_HOME.'
}

function Assert-GradleWrapper {
    if (Test-Path $GradleBat) { return }
    throw "Gradle wrapper not found at $GradleBat. The wrapper is committed to the repository; " +
          'restore it rather than installing Gradle by hand, so every machine builds with the same version.'
}

$sdk = Resolve-AndroidSdk
$javaHome = Resolve-JavaHome
Assert-GradleWrapper

$variant = $Configuration.ToLowerInvariant()
$assembleTask = if ($Configuration -eq 'Release') { 'assembleRelease' } else { 'assembleDebug' }
$lintTask = if ($Configuration -eq 'Release') { 'lintRelease' } else { 'lintDebug' }
$apkDirectory = Join-Path $AndroidRoot "app\build\outputs\apk\$variant"
$apkCandidates = if ($Configuration -eq 'Release') {
    @(
        (Join-Path $apkDirectory 'app-release.apk'),
        (Join-Path $apkDirectory 'app-release-unsigned.apk')
    )
} else {
    @((Join-Path $apkDirectory 'app-debug.apk'))
}
$apkTargetName = if ($Configuration -eq 'Release') {
    "PocketMic-v$AppVersion-release-unsigned.apk"
} else {
    "PocketMic-v$AppVersion-debug.apk"
}
$apkTarget = Join-Path $Root "release\$apkTargetName"

Write-Host "Android SDK: $sdk"
Write-Host "JAVA_HOME:   $javaHome"
Write-Host "Gradle:      wrapper ($GradleBat)"

Push-Location $AndroidRoot
try {
    $arguments = @('--no-daemon', '--stacktrace', '--console=plain')
    if (-not $SkipClean) { $arguments += 'clean' }
    if (-not $SkipChecks) {
        $arguments += 'testDebugUnitTest'
        $arguments += $lintTask
    }
    $arguments += $assembleTask

    & $GradleBat @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Gradle exited with code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

$apkSource = $apkCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $apkSource) {
    throw "Build completed but no APK was found. Checked: $($apkCandidates -join ', ')"
}

New-Item -ItemType Directory -Force (Split-Path -Parent $apkTarget) | Out-Null
Copy-Item $apkSource $apkTarget -Force
Write-Host "Built: $apkTarget"
