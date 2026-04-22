param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    [ValidateSet("Development","QA","Production")]
    [string]$Environment = "Production",
    [switch]$PublishOnly,
    [switch]$BuildMsi
)

$ErrorActionPreference = "Stop"
$SolutionDir = Split-Path $PSScriptRoot -Parent
$PublishDir = (Join-Path $SolutionDir "Publish").TrimEnd('\')
$UpdaterOutput = (Join-Path $SolutionDir "Publish_Updater").TrimEnd('\')
$OutputDir = Join-Path $SolutionDir "releases"

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Error "버전 형식이 잘못되었습니다. 예: 1.0.0"
    exit 1
}

$Version4 = "$Version.0"

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "빌드 버전: $Version" -ForegroundColor Cyan
Write-Host "PublishDir : $PublishDir" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# Clean
$pathsToClean = @(
    "$SolutionDir\Application\LGCNS.axink.App\bin",
    "$SolutionDir\Application\LGCNS.axink.App\obj",
    "$SolutionDir\Setup\LGCNS.axink.MSI\bin",
    "$SolutionDir\Setup\LGCNS.axink.MSI\obj",
    "$SolutionDir\Updater\LGCNS.axink.Updater\bin",
    "$SolutionDir\Updater\LGCNS.axink.Updater\obj",
    $PublishDir
)

foreach ($path in $pathsToClean) {
    if (Test-Path $path) {
        Remove-Item -Path $path -Recurse -Force
    }
}

# 1. Publish
Write-Host "`n[1/4] Publishing WPF App..." -ForegroundColor Yellow

dotnet publish "$SolutionDir\Application\LGCNS.axink.App\LGCNS.axink.App.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -p:FileVersion=$Version4 `
    -p:AssemblyVersion=$Version4 `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n[2/4] Publishing Updater..." -ForegroundColor Yellow

dotnet publish "$SolutionDir\Updater\LGCNS.axink.Updater\LGCNS.axink.Updater.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:Version=$Version `
    -p:FileVersion=$Version4 `
    -p:AssemblyVersion=$Version4 `
    -o $UpdaterOutput
 
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# EXE만 메인 PublishDir로 복사
Copy-Item "$UpdaterOutput\axink Translator Updater.exe" $PublishDir
Remove-Item $UpdaterOutput -Recurse -Force

# Updater.exe가 PublishDir에 존재하는지 확인
$updaterExe = Join-Path $PublishDir "axink Translator Updater.exe"
if (Test-Path $updaterExe) {
    Write-Host "  Updater OK: $(Split-Path $updaterExe -Leaf)" -ForegroundColor Green
} else {
    Write-Error "Updater.exe가 PublishDir에 생성되지 않았습니다."
    exit 1
}


if ($PublishOnly) {
    Write-Host "`nPublish 완료: $PublishDir" -ForegroundColor Green
    exit 0
}

# 2. appsettings.json 환경별 병합
Write-Host "`n[3/4] 환경 설정 병합: $Environment" -ForegroundColor Yellow

$baseConfig = Join-Path $PublishDir "appsettings.json"
$envConfig  = Join-Path $PublishDir "appsettings.$Environment.json"

if (Test-Path $envConfig) {
    $base = Get-Content $baseConfig -Raw | ConvertFrom-Json
    $env  = Get-Content $envConfig  -Raw | ConvertFrom-Json

    foreach ($prop in $env.AppSettings.PSObject.Properties) {
        if ($prop.Value) {
            $base.AppSettings | Add-Member -Force -MemberType NoteProperty -Name $prop.Name -Value $prop.Value
        }
    }

    $base | ConvertTo-Json -Depth 10 | Set-Content $baseConfig -Encoding UTF8
    Write-Host "  병합 완료: $baseConfig" -ForegroundColor Green
} else {
    Write-Host "  환경별 파일 없음, 기본값 사용" -ForegroundColor Gray
}

# 환경별 파일 제거 (배포에 불필요)
Remove-Item "$PublishDir\appsettings.*.json" -Force -ErrorAction SilentlyContinue
Write-Host "  환경별 파일 정리 완료" -ForegroundColor Green

if ($PublishOnly) {
    Write-Host "`nPublish 완료: $PublishDir" -ForegroundColor Green
    exit 0
}

# MSI 빌드
if ($BuildMsi) {
    Write-Host "`n[4/4] Building MSI..." -ForegroundColor Yellow
    
    dotnet build "$SolutionDir\Setup\LGCNS.axink.MSI\LGCNS.axink.MSI.wixproj" `
        -c Release `
        -p:Platform=x64 `
        -p:Version=$Version4 `
        -p:PublishDir="$PublishDir" `
        -p:AppEnvironment=$Environment
    
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    # MSI를 releases 폴더로 복사
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

    $msiFile = Get-ChildItem "$SolutionDir\Setup\LGCNS.axink.MSI\bin\x64\Release\ko-KR\*.msi" |
               Select-Object -First 1

    if ($msiFile) {
        Copy-Item $msiFile.FullName $OutputDir
        Write-Host "`nMSI 복사 완료: $OutputDir\$($msiFile.Name)" -ForegroundColor Green
    } else {
        Write-Error "MSI 파일을 찾을 수 없습니다."
        exit 1
    }
}