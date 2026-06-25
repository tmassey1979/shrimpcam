param(
    [switch]$EnforceCoverage
)

$ErrorActionPreference = "Stop"

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Command
    )

    $global:LASTEXITCODE = 0
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $Command"
    }
}

Invoke-Checked { dotnet restore ShrimpCam.sln }
Invoke-Checked { dotnet build ShrimpCam.sln --no-restore }
Invoke-Checked { dotnet format ShrimpCam.sln --verify-no-changes --severity warn --no-restore }
Invoke-Checked { dotnet test ShrimpCam.sln --no-build }

if ($EnforceCoverage) {
    & (Join-Path $PSScriptRoot "enforce-backend-coverage.ps1") -Threshold 90
}

Push-Location src/ShrimpCam.Web
try {
    if (-not (Test-Path -LiteralPath node_modules)) {
        Invoke-Checked { npm ci }
    }

    Invoke-Checked { npm run check }
    Invoke-Checked { npm run build }
    Invoke-Checked { npm run e2e:install }
    Invoke-Checked { npm run e2e }
}
finally {
    Pop-Location
}
