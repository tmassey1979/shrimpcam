param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot ".."))
)

$ErrorActionPreference = "Stop"

$versionPath = Join-Path $RepositoryRoot "VERSION"
$packagePath = Join-Path $RepositoryRoot "src/ShrimpCam.Web/package.json"
$packageLockPath = Join-Path $RepositoryRoot "src/ShrimpCam.Web/package-lock.json"

if (-not (Test-Path -LiteralPath $versionPath)) {
    throw "Missing VERSION file."
}

$version = (Get-Content -LiteralPath $versionPath -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') {
    throw "VERSION '$version' is not a supported SemVer value."
}

$package = Get-Content -LiteralPath $packagePath -Raw | ConvertFrom-Json
if ($package.version -ne $version) {
    throw "src/ShrimpCam.Web/package.json version '$($package.version)' does not match VERSION '$version'."
}

$packageLockJson = Get-Content -LiteralPath $packageLockPath -Raw
if ($packageLockJson -notmatch '"version"\s*:\s*"' + [Regex]::Escape($version) + '"') {
    throw "src/ShrimpCam.Web/package-lock.json top-level version does not match VERSION '$version'."
}

if ($packageLockJson -notmatch '"packages"\s*:\s*\{\s*""\s*:\s*\{[^}]*"version"\s*:\s*"' + [Regex]::Escape($version) + '"') {
    throw "src/ShrimpCam.Web/package-lock.json root package version does not match VERSION '$version'."
}

Write-Host "Shrimp Cam version metadata is consistent at $version."
