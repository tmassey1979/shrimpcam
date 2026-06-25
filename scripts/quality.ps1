param(
    [switch]$EnforceCoverage
)

$ErrorActionPreference = "Stop"

dotnet restore ShrimpCam.sln
dotnet build ShrimpCam.sln --no-restore
dotnet format ShrimpCam.sln --verify-no-changes --severity warn --no-restore
dotnet test ShrimpCam.sln --no-build

if ($EnforceCoverage) {
    powershell -ExecutionPolicy Bypass -File scripts/enforce-backend-coverage.ps1 -Threshold 90
}

Push-Location src/ShrimpCam.Web
try {
    if (-not (Test-Path -LiteralPath node_modules)) {
        npm install
    }

    npm run check
    npm run build
}
finally {
    Pop-Location
}
