#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 5 ]]; then
  echo "usage: build-pi-image.sh <image-url> <image-sha256> <api-publish-dir> <web-dist-dir> <output-dir>" >&2
  exit 1
fi

IMAGE_URL="$1"
IMAGE_SHA256="$2"
API_DIR="$(realpath "$3")"
WEB_DIR="$(realpath "$4")"
OUTPUT_DIR="$(realpath -m "$5")"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
DEPLOY_DIR="${REPO_ROOT}/deploy/raspberry-pi"
WORK_DIR="$(mktemp -d)"
ARCHIVE_PATH="${WORK_DIR}/raspios.img.download"
RAW_IMAGE_PATH="${WORK_DIR}/raspios.img"
BOOT_MNT="${WORK_DIR}/boot"
ROOT_MNT="${WORK_DIR}/root"
IMAGE_BASENAME="shrimpcam-raspios-lite-$(date -u +%Y%m%d%H%M%S)"

cleanup() {
  set +e
  if mountpoint -q "${BOOT_MNT}"; then
    sudo umount "${BOOT_MNT}"
  fi
  if mountpoint -q "${ROOT_MNT}"; then
    sudo umount "${ROOT_MNT}"
  fi
  if [[ -n "${LOOP_DEVICE:-}" ]]; then
    sudo losetup -d "${LOOP_DEVICE}"
  fi
  rm -rf "${WORK_DIR}"
}
trap cleanup EXIT

mkdir -p "${OUTPUT_DIR}" "${BOOT_MNT}" "${ROOT_MNT}"

curl -L "${IMAGE_URL}" -o "${ARCHIVE_PATH}"
printf '%s  %s\n' "${IMAGE_SHA256}" "${ARCHIVE_PATH}" | sha256sum --check --status

cat > "${OUTPUT_DIR}/base-image-provenance.json" <<EOF
{
  "imageUrl": "${IMAGE_URL}",
  "sha256": "${IMAGE_SHA256}",
  "verifiedAtUtc": "$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
}
EOF

if file "${ARCHIVE_PATH}" | grep -qi "Zip archive"; then
  unzip -q "${ARCHIVE_PATH}" -d "${WORK_DIR}/unzipped"
  RAW_IMAGE_PATH="$(find "${WORK_DIR}/unzipped" -name '*.img' | head -n 1)"
elif file "${ARCHIVE_PATH}" | grep -qi "XZ compressed"; then
  mv "${ARCHIVE_PATH}" "${ARCHIVE_PATH}.xz"
  xz -d "${ARCHIVE_PATH}.xz"
  mv "${ARCHIVE_PATH}" "${RAW_IMAGE_PATH}"
else
  mv "${ARCHIVE_PATH}" "${RAW_IMAGE_PATH}"
fi

LOOP_DEVICE="$(sudo losetup --find --show --partscan "${RAW_IMAGE_PATH}")"
BOOT_PARTITION="${LOOP_DEVICE}p1"
ROOT_PARTITION="${LOOP_DEVICE}p2"

sudo mount "${BOOT_PARTITION}" "${BOOT_MNT}"
sudo mount "${ROOT_PARTITION}" "${ROOT_MNT}"

sudo mkdir -p \
  "${ROOT_MNT}/opt/shrimpcam/api" \
  "${ROOT_MNT}/opt/shrimpcam/web" \
  "${ROOT_MNT}/opt/shrimpcam/install/nginx" \
  "${ROOT_MNT}/usr/local/bin" \
  "${ROOT_MNT}/etc/default" \
  "${ROOT_MNT}/etc/systemd/system/multi-user.target.wants"

sudo rsync -a "${API_DIR}/" "${ROOT_MNT}/opt/shrimpcam/api/"
sudo rsync -a "${WEB_DIR}/" "${ROOT_MNT}/opt/shrimpcam/web/"

sudo install -m 0644 "${DEPLOY_DIR}/shrimpcam-api.service" "${ROOT_MNT}/etc/systemd/system/shrimpcam-api.service"
sudo install -m 0644 "${DEPLOY_DIR}/shrimpcam-firstboot.service" "${ROOT_MNT}/etc/systemd/system/shrimpcam-firstboot.service"
sudo install -m 0755 "${DEPLOY_DIR}/firstboot-provision.sh" "${ROOT_MNT}/usr/local/bin/shrimpcam-firstboot.sh"
sudo install -m 0644 "${DEPLOY_DIR}/nginx-shrimpcam.conf" "${ROOT_MNT}/opt/shrimpcam/install/nginx/shrimpcam.conf"

cat <<EOF | sudo tee "${ROOT_MNT}/etc/default/shrimpcam-api" > /dev/null
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:5000
EOF

sudo ln -sf /etc/systemd/system/shrimpcam-api.service "${ROOT_MNT}/etc/systemd/system/multi-user.target.wants/shrimpcam-api.service"
sudo ln -sf /etc/systemd/system/shrimpcam-firstboot.service "${ROOT_MNT}/etc/systemd/system/multi-user.target.wants/shrimpcam-firstboot.service"

cat > "${WORK_DIR}/shrimpcam-device.env" <<EOF
# Edit these values before first boot if you are not injecting GitHub secrets in the workflow.
SHRIMPCAM_HOSTNAME=${SHRIMPCAM_HOSTNAME:-shrimpcam}
SHRIMPCAM_WIFI_SSID=${SHRIMPCAM_WIFI_SSID:-}
SHRIMPCAM_WIFI_PSK=${SHRIMPCAM_WIFI_PSK:-}
SHRIMPCAM_WIFI_COUNTRY=${SHRIMPCAM_WIFI_COUNTRY:-US}
EOF

sudo cp "${WORK_DIR}/shrimpcam-device.env" "${BOOT_MNT}/shrimpcam-device.env"
sudo touch "${BOOT_MNT}/ssh"

sync
sudo umount "${BOOT_MNT}"
sudo umount "${ROOT_MNT}"
sudo losetup -d "${LOOP_DEVICE}"
unset LOOP_DEVICE

cp "${RAW_IMAGE_PATH}" "${OUTPUT_DIR}/${IMAGE_BASENAME}.img"
xz -T0 -z "${OUTPUT_DIR}/${IMAGE_BASENAME}.img"

cp "${DEPLOY_DIR}/README.md" "${OUTPUT_DIR}/README.md"
