# Shrimp Cam Windows Service

This folder contains helper scripts for running Shrimp Cam as a Windows service.

## Publish

From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\publish-shrimpcam.ps1 -Runtime win-x64 -OutputRoot artifacts\publish\windows
```

Copy the published `api` folder to `C:\Program Files\ShrimpCam\api`.

## Install

Run PowerShell as administrator:

```powershell
powershell -ExecutionPolicy Bypass -File deploy\windows\install-shrimpcam-service.ps1
```

The installer creates the `ShrimpCam` service with automatic startup and restart-on-failure recovery. It stores runtime data below `C:\ProgramData\ShrimpCam` by default.

## Uninstall

```powershell
powershell -ExecutionPolicy Bypass -File deploy\windows\uninstall-shrimpcam-service.ps1
```
