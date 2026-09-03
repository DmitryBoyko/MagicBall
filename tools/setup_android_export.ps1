# Prepare Android export: Yandex AARs, SDK path, Gradle suppress flag.
# Run from repo root before Godot export:
#   .\tools\setup_android_export.ps1
param(
    [string]$SdkPath = "$env:LOCALAPPDATA\Android\Sdk"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$AddonBin = Join-Path $Root "addons\GodotAndroidYandexAds\bin"
$AndroidBuild = Join-Path $Root "android\build"
$GradleProps = Join-Path $AndroidBuild "gradle.properties"
$LocalProps = Join-Path $AndroidBuild "local.properties"
$YandexZip = Join-Path $env:TEMP "magicalball-yandex-addons.zip"
$YandexUrl = "https://github.com/noctisalamandra/godot-yandex-ads-android/releases/download/v1.3/addons.zip"

function Ensure-YandexAars {
    $debugAar = Join-Path $AddonBin "debug\GodotAndroidYandexAds-debug.aar"
    $releaseAar = Join-Path $AddonBin "release\GodotAndroidYandexAds-release.aar"
    if ((Test-Path $debugAar) -and (Test-Path $releaseAar)) {
        Write-Host "OK - Yandex AARs present"
        return
    }

    Write-Host "Downloading Yandex plugin AARs..."
    Invoke-WebRequest -Uri $YandexUrl -OutFile $YandexZip
    $extract = Join-Path $env:TEMP "magicalball-yandex-addons"
    if (Test-Path $extract) { Remove-Item -Recurse -Force $extract }
    Expand-Archive -Force $YandexZip $extract

    New-Item -ItemType Directory -Force -Path (Join-Path $AddonBin "debug"), (Join-Path $AddonBin "release") | Out-Null
    Copy-Item (Join-Path $extract "addons\GodotAndroidYandexAds\bin\debug\*.aar") (Join-Path $AddonBin "debug") -Force
    Copy-Item (Join-Path $extract "addons\GodotAndroidYandexAds\bin\release\*.aar") (Join-Path $AddonBin "release") -Force
    Write-Host "OK - Yandex AARs installed"
}

function Ensure-AndroidTemplate {
    if (-not (Test-Path $GradleProps)) {
        Write-Error @"
Android build template missing: $AndroidBuild
In Godot: Project -> Install Android Build Template...
Then re-run this script.
"@
    }
}

function Ensure-Sdk {
    if (-not (Test-Path $SdkPath)) {
        Write-Error "Android SDK not found: $SdkPath`nInstall via Android Studio or set -SdkPath."
    }
    $platform36 = Join-Path $SdkPath "platforms\android-36"
    if (-not (Test-Path $platform36)) {
        Write-Warning "android-36 platform missing. In Android Studio SDK Manager install Android 16 (API 36)."
    }
    $sdkLine = "sdk.dir=$($SdkPath -replace '\\','/')"
    Set-Content -Encoding ASCII -Path $LocalProps -Value $sdkLine
    Write-Host "OK - $LocalProps"
}

function Find-Jdk17 {
    $candidates = @(
        "C:\Program Files\Eclipse Adoptium\jdk-17*",
        "C:\Program Files\Java\jdk-17*",
        "C:\Program Files\Microsoft\jdk-17*"
    )
    foreach ($pattern in $candidates) {
        $hit = Get-ChildItem $pattern -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
        if ($hit) { return $hit.FullName }
    }
    return $null
}

function Ensure-Jdk17 {
    $jdk = Find-Jdk17
    if (-not $jdk) {
        Write-Warning "JDK 17 not found. Android Gradle needs Java 17 (Java 25 breaks lint)."
        Write-Warning "Install: winget install EclipseAdoptium.Temurin.17.JDK"
        return
    }
    $jdkUnix = ($jdk -replace '\\', '/')
    $content = Get-Content $GradleProps -Raw
    if ($content -match 'org\.gradle\.java\.home=') {
        $content = $content -replace 'org\.gradle\.java\.home=.*', "org.gradle.java.home=$jdkUnix"
        Set-Content -Path $GradleProps -Value $content.TrimEnd()
    } else {
        Add-Content -Path $GradleProps -Value "org.gradle.java.home=$jdkUnix"
    }
    Write-Host "OK - org.gradle.java.home=$jdk"
    Write-Host "Godot: Editor Settings -> Export -> Android -> Java SDK Path -> $jdk"
}

function Ensure-GradleSuppress {
    $content = Get-Content $GradleProps -Raw
    if ($content -notmatch 'android\.suppressUnsupportedCompileSdk=36') {
        Add-Content -Path $GradleProps -Value "`nandroid.suppressUnsupportedCompileSdk=36"
    }
    Write-Host "OK - gradle suppressUnsupportedCompileSdk=36"
}

function Ensure-OnnxAar {
    $dest = Join-Path $Root "addons\OnnxRuntimeAndroid\bin\onnxruntime.aar"
    $src = Join-Path $Root "packages\microsoft.ml.onnxruntime\1.19.0\runtimes\android\native\onnxruntime.aar"
    if (-not (Test-Path $src)) {
        Write-Warning "ONNX package missing. Run: dotnet restore MagicBall.csproj"
        return
    }
    New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null
    Copy-Item $src $dest -Force
    Write-Host "OK - onnxruntime.aar"
}

    $sln = Join-Path $Root "MagicBall.sln"
    if (-not (Test-Path $sln)) {
        Write-Error "Missing $sln"
    }
    Push-Location $Root
    try {
        dotnet build $sln -c ExportRelease | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }
        Write-Host "OK - dotnet ExportRelease"
    } finally {
        Pop-Location
    }
}

Write-Host "=== MagicalBall Android export setup ==="
Ensure-AndroidTemplate
Ensure-YandexAars
Ensure-OnnxAar
Ensure-Sdk
Ensure-GradleSuppress
Ensure-Jdk17
Ensure-Dotnet
Write-Host ""
Write-Host "Ready. In Godot: Project -> Export -> Android -> Export Project."
Write-Host "Keystore: .\tools\setup_export_credentials.ps1"
