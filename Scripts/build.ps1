param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    [switch]$PublishOnly,
    [switch]$BuildMsi
)

$ErrorActionPreference = "Stop"
$SolutionDir = Split-Path $PSScriptRoot -Parent
$PublishDir = (Join-Path $SolutionDir "Publish").TrimEnd('\')
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
    $PublishDir
)

foreach ($path in $pathsToClean) {
    if (Test-Path $path) {
        Remove-Item -Path $path -Recurse -Force
    }
}

# 1. Publish
Write-Host "`n[1/3] Publishing app..." -ForegroundColor Yellow

dotnet publish "$SolutionDir\Application\LGCNS.axink.App\LGCNS.axink.App.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -p:FileVersion=$Version4 `
    -p:AssemblyVersion=$Version4 `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($PublishOnly) {
    Write-Host "`nPublish 완료: $PublishDir" -ForegroundColor Green
    exit 0
}

# 2. MSI 빌드
if ($BuildMsi) {
    Write-Host "`n[2/3] Building MSI..." -ForegroundColor Yellow
    
    dotnet build "$SolutionDir\Setup\LGCNS.axink.MSI\LGCNS.axink.MSI.wixproj" `
        -c Release `
        -p:Platform=x64 `
        -p:Version=$Version4 `
        -p:PublishDir="$PublishDir"
    
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}