param(
    [string]$Runtime = "linux-arm64",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "artifacts/publish/pi"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$outputRootPath = Join-Path $repoRoot $OutputRoot
$apiOutputPath = Join-Path $outputRootPath "api"
$webOutputPath = Join-Path $outputRootPath "web"
$apiWebRootPath = Join-Path $repoRoot "src/ShrimpCam.Api/wwwroot"

if (Test-Path -LiteralPath $outputRootPath) {
    Remove-Item -LiteralPath $outputRootPath -Recurse -Force
}

New-Item -ItemType Directory -Path $apiOutputPath | Out-Null
New-Item -ItemType Directory -Path $webOutputPath | Out-Null
if (Test-Path -LiteralPath $apiWebRootPath) {
    Remove-Item -LiteralPath $apiWebRootPath -Recurse -Force
}
New-Item -ItemType Directory -Path $apiWebRootPath | Out-Null

Push-Location (Join-Path $repoRoot "src/ShrimpCam.Web")
try {
    if (-not (Test-Path -LiteralPath "node_modules")) {
        npm ci
    }

    npm run build
    Copy-Item -Path "dist\*" -Destination $webOutputPath -Recurse
    Copy-Item -Path "dist\*" -Destination $apiWebRootPath -Recurse
}
finally {
    Pop-Location
}

dotnet publish (Join-Path $repoRoot "src/ShrimpCam.Api/ShrimpCam.Api.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    -o $apiOutputPath
