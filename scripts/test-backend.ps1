param(
    [ValidateSet("All", "Unit", "Integration", "Api")]
    [string]$Category = "All",
    [switch]$IncludeHardware
)

$ErrorActionPreference = "Stop"

$projects = switch ($Category) {
    "Unit" {
        @("tests/ShrimpCam.Core.Tests/ShrimpCam.Core.Tests.csproj")
        break
    }
    "Integration" {
        @("tests/ShrimpCam.Infrastructure.Tests/ShrimpCam.Infrastructure.Tests.csproj")
        break
    }
    "Api" {
        @("tests/ShrimpCam.Api.Tests/ShrimpCam.Api.Tests.csproj")
        break
    }
    default {
        @(
            "tests/ShrimpCam.Core.Tests/ShrimpCam.Core.Tests.csproj",
            "tests/ShrimpCam.Infrastructure.Tests/ShrimpCam.Infrastructure.Tests.csproj",
            "tests/ShrimpCam.Api.Tests/ShrimpCam.Api.Tests.csproj"
        )
    }
}

$filterParts = @()

if ($Category -ne "All") {
    $filterParts += "Category=$Category"
}

if (-not $IncludeHardware) {
    $filterParts += "Category!=Hardware"
}

$filter = if ($filterParts.Count -gt 0) {
    [string]::Join("&", $filterParts)
}
else {
    $null
}

foreach ($project in $projects) {
    if ($null -ne $filter) {
        dotnet test $project --no-restore --filter $filter
    }
    else {
        dotnet test $project --no-restore
    }
}
