# Shrimp Cam Raspberry Pi Image

This image is built from Raspberry Pi OS Lite so it boots without a GUI.

## What the image does

- Boots into a headless Raspberry Pi OS Lite environment.
- Starts `shrimpcam-api.service` automatically on startup.
- Runs a first-boot provisioning service that:
  - applies hostname and Wi-Fi settings from `shrimpcam-device.env`
  - runs before `network-online.target` so headless Wi-Fi configuration is available before services wait for connectivity
  - installs `ffmpeg`, `v4l-utils`, and `nginx-light`
  - configures `nginx` to serve the PWA and proxy API routes
  - enables the Shrimp Cam API and `nginx` services for future boots

## Wi-Fi configuration

Before the first boot, edit `shrimpcam-device.env` on the boot partition if the workflow did not inject your Wi-Fi credentials from GitHub secrets.

Required values:

- `SHRIMPCAM_HOSTNAME`
- `SHRIMPCAM_WIFI_SSID`
- `SHRIMPCAM_WIFI_PSK`
- `SHRIMPCAM_WIFI_COUNTRY`

The image builder also drops an empty `ssh` marker file on the boot partition so SSH is available on first boot.

## Release workflow guardrails

The Raspberry Pi image workflow publishes alpha release artifacts only after the `ci` workflow completes successfully on `main`. Manual workflow runs can still build and upload artifacts for validation, but they do not create GitHub releases automatically.

The workflow accepts only the official Raspberry Pi OS Lite arm64 image URL by default so release artifacts are built from a predictable base image source.
