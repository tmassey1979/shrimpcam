param(
    [int]$Threshold = 90
)

$ErrorActionPreference = "Stop"

function Resolve-CoverageFilePath {
    param(
        [string]$FilePath,
        [string[]]$SourceRoots
    )

    if ([string]::IsNullOrWhiteSpace($FilePath)) {
        return "<unknown>"
    }

    if ([IO.Path]::IsPathRooted($FilePath)) {
        return [IO.Path]::GetFullPath($FilePath)
    }

    if ($SourceRoots.Count -gt 0) {
        return [IO.Path]::GetFullPath((Join-Path $SourceRoots[0] $FilePath))
    }

    return $FilePath
}

function Get-LineRate {
    param(
        [int]$CoveredLines,
        [int]$ValidLines
    )

    if ($ValidLines -eq 0) {
        return 100
    }

    return [math]::Round(($CoveredLines / $ValidLines) * 100, 1)
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$resultsRoot = Join-Path $repoRoot "tests\TestResults\CoverageGate"
$summaryPath = Join-Path $resultsRoot "backend-coverage-summary.md"
$jsonPath = Join-Path $resultsRoot "backend-coverage-summary.json"

$testProjects = @(
    @{
        Name = "ShrimpCam.Core.Tests"
        Path = "tests/ShrimpCam.Core.Tests/ShrimpCam.Core.Tests.csproj"
    },
    @{
        Name = "ShrimpCam.Infrastructure.Tests"
        Path = "tests/ShrimpCam.Infrastructure.Tests/ShrimpCam.Infrastructure.Tests.csproj"
    },
    @{
        Name = "ShrimpCam.Api.Tests"
        Path = "tests/ShrimpCam.Api.Tests/ShrimpCam.Api.Tests.csproj"
    }
)

if (Test-Path -LiteralPath $resultsRoot) {
    Remove-Item -LiteralPath $resultsRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $resultsRoot | Out-Null

foreach ($project in $testProjects) {
    $projectResults = Join-Path $resultsRoot $project.Name
    New-Item -ItemType Directory -Path $projectResults | Out-Null

    dotnet test $project.Path --no-build --settings tests/coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory $projectResults

    if ($LASTEXITCODE -ne 0) {
        throw "Coverage collection failed for $($project.Name)."
    }
}

$coverageFiles = Get-ChildItem -Path $resultsRoot -Recurse -Filter "coverage.cobertura.xml"

if ($coverageFiles.Count -eq 0) {
    throw "No coverage reports were produced under '$resultsRoot'."
}

$lineCoverage = @{}

foreach ($coverageFile in $coverageFiles) {
    [xml]$coverageDocument = Get-Content -LiteralPath $coverageFile.FullName
    $sourceRoots = @(
        $coverageDocument.coverage.sources.source |
            ForEach-Object { "$_" } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )

    foreach ($package in @($coverageDocument.coverage.packages.package)) {
        foreach ($class in @($package.classes.class)) {
            $normalizedFile = Resolve-CoverageFilePath -FilePath $class.filename -SourceRoots $sourceRoots

            foreach ($line in @($class.lines.line)) {
                $key = "{0}|{1}|{2}" -f $package.name, $normalizedFile, $line.number
                $hits = [int]$line.hits

                if ($lineCoverage.ContainsKey($key)) {
                    $lineCoverage[$key].Hits = [Math]::Max($lineCoverage[$key].Hits, $hits)
                    continue
                }

                $lineCoverage[$key] = [pscustomobject]@{
                    Module = [string]$package.name
                    File = $normalizedFile
                    Line = [int]$line.number
                    Hits = $hits
                }
            }
        }
    }
}

$moduleCoverage = foreach ($moduleGroup in ($lineCoverage.Values | Group-Object Module | Sort-Object Name)) {
    $validLines = $moduleGroup.Count
    $coveredLines = @($moduleGroup.Group | Where-Object { $_.Hits -gt 0 }).Count
    $lineRate = Get-LineRate -CoveredLines $coveredLines -ValidLines $validLines

    [pscustomobject]@{
        Module = $moduleGroup.Name
        CoveredLines = $coveredLines
        ValidLines = $validLines
        LineRate = $lineRate
    }
}

$overallCoveredLines = @($lineCoverage.Values | Where-Object { $_.Hits -gt 0 }).Count
$overallValidLines = $lineCoverage.Count
$overallLineRate = Get-LineRate -CoveredLines $overallCoveredLines -ValidLines $overallValidLines

$coreCoverage = $moduleCoverage | Where-Object Module -eq "ShrimpCam.Core"

if ($null -eq $coreCoverage) {
    throw "Combined coverage summary did not include the ShrimpCam.Core module."
}

$summaryLines = [System.Collections.Generic.List[string]]::new()
$summaryLines.Add("# Backend Coverage Summary")
$summaryLines.Add("")
$summaryLines.Add("| Scope | Covered Lines | Valid Lines | Line Coverage | Threshold |")
$summaryLines.Add("| --- | --- | --- | --- | --- |")
$summaryLines.Add("| ShrimpCam.Core | $($coreCoverage.CoveredLines) | $($coreCoverage.ValidLines) | $($coreCoverage.LineRate)% | $Threshold% |")
$summaryLines.Add("| Overall Backend | $overallCoveredLines | $overallValidLines | $overallLineRate% | $Threshold% |")
$summaryLines.Add("")
$summaryLines.Add("## Module Breakdown")
$summaryLines.Add("")
$summaryLines.Add("| Module | Covered Lines | Valid Lines | Line Coverage |")
$summaryLines.Add("| --- | --- | --- | --- |")

foreach ($module in $moduleCoverage) {
    $summaryLines.Add("| $($module.Module) | $($module.CoveredLines) | $($module.ValidLines) | $($module.LineRate)% |")
}

$summaryPayload = [pscustomobject]@{
    threshold = $Threshold
    generatedOn = (Get-Date).ToString("o")
    overall = [pscustomobject]@{
        coveredLines = $overallCoveredLines
        validLines = $overallValidLines
        lineRate = $overallLineRate
    }
    modules = $moduleCoverage
}

Set-Content -LiteralPath $summaryPath -Value $summaryLines -Encoding utf8
$summaryPayload | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $jsonPath -Encoding utf8

Write-Host ""
Write-Host "Backend coverage summary"
Write-Host "  ShrimpCam.Core: $($coreCoverage.LineRate)% ($($coreCoverage.CoveredLines)/$($coreCoverage.ValidLines))"
Write-Host "  Overall backend: $overallLineRate% ($overallCoveredLines/$overallValidLines)"
Write-Host "  Markdown summary: $summaryPath"
Write-Host "  JSON summary: $jsonPath"
Write-Host ""

$coverageFailures = @()

if ($coreCoverage.LineRate -lt $Threshold) {
    $coverageFailures += "ShrimpCam.Core line coverage $($coreCoverage.LineRate)% is below the required $Threshold%."
}

if ($overallLineRate -lt $Threshold) {
    $coverageFailures += "Overall backend line coverage $overallLineRate% is below the required $Threshold%."
}

if ($coverageFailures.Count -gt 0) {
    throw ($coverageFailures -join " ")
}
