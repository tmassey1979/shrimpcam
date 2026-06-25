param(
    [string]$InstallRoot = "C:\Program Files\ShrimpCam",
    [string]$DataRoot = "C:\ProgramData\ShrimpCam",
    [string]$ServiceName = "ShrimpCam",
    [string]$DisplayName = "Shrimp Cam",
    [int]$Port = 8080
)

$ErrorActionPreference = "Stop"

$apiPath = Join-Path $InstallRoot "api\ShrimpCam.Api.exe"
if (-not (Test-Path -LiteralPath $apiPath)) {
    throw "Shrimp Cam API executable was not found at '$apiPath'. Publish the Windows package before installing the service."
}

$imageRoot = Join-Path $DataRoot "images"
$timelapseRoot = Join-Path $DataRoot "timelapse"
$databasePath = Join-Path $DataRoot "data\shrimpcam.db"
$logRoot = Join-Path $DataRoot "logs"

foreach ($path in @($DataRoot, (Split-Path -Parent $databasePath), $imageRoot, $timelapseRoot, $logRoot)) {
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

$environment = [Environment]::GetEnvironmentVariables("Machine")
$environment["ASPNETCORE_URLS"] = "http://0.0.0.0:$Port"
$environment["ShrimpCam__Storage__DatabasePath"] = $databasePath
$environment["ShrimpCam__Storage__ImageRootPath"] = $imageRoot
$environment["ShrimpCam__Storage__TimelapseRootPath"] = $timelapseRoot

foreach ($key in $environment.Keys) {
    if ($key -like "ShrimpCam__*" -or $key -eq "ASPNETCORE_URLS") {
        [Environment]::SetEnvironmentVariable($key, [string]$environment[$key], "Machine")
    }
}

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    sc.exe stop $ServiceName | Out-Null
    sc.exe delete $ServiceName | Out-Null
}

New-Service `
    -Name $ServiceName `
    -DisplayName $DisplayName `
    -BinaryPathName "`"$apiPath`"" `
    -StartupType Automatic `
    -Description "Shrimp Cam backend and PWA host"

sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/5000/restart/30000 | Out-Null
Start-Service -Name $ServiceName
