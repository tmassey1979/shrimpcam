#!/usr/bin/env bash
set -euo pipefail

CONFIG_PATH=""
for candidate in /boot/firmware/shrimpcam-device.env /boot/shrimpcam-device.env; do
  if [[ -f "${candidate}" ]]; then
    CONFIG_PATH="${candidate}"
    break
  fi
done

if [[ -z "${CONFIG_PATH}" ]]; then
  echo "shrimpcam-device.env was not found on the boot partition" >&2
  exit 1
fi

# shellcheck disable=SC1090
source "${CONFIG_PATH}"

HOSTNAME_VALUE="${SHRIMPCAM_HOSTNAME:-shrimpcam}"
WIFI_SSID="${SHRIMPCAM_WIFI_SSID:-}"
WIFI_PSK="${SHRIMPCAM_WIFI_PSK:-}"
WIFI_COUNTRY="${SHRIMPCAM_WIFI_COUNTRY:-US}"

hostnamectl set-hostname "${HOSTNAME_VALUE}"

if [[ -n "${WIFI_SSID}" && -n "${WIFI_PSK}" ]]; then
  if [[ -d /etc/NetworkManager/system-connections ]]; then
    install -d -m 700 /etc/NetworkManager/system-connections
    cat > /etc/NetworkManager/system-connections/shrimpcam-wifi.nmconnection <<EOF
[connection]
id=shrimpcam-wifi
type=wifi
autoconnect=true

[wifi]
mode=infrastructure
ssid=${WIFI_SSID}

[wifi-security]
key-mgmt=wpa-psk
psk=${WIFI_PSK}

[ipv4]
method=auto

[ipv6]
method=ignore
EOF
    chmod 600 /etc/NetworkManager/system-connections/shrimpcam-wifi.nmconnection
    systemctl restart NetworkManager || true
  else
    install -d -m 755 /etc/wpa_supplicant
    cat > /etc/wpa_supplicant/wpa_supplicant.conf <<EOF
country=${WIFI_COUNTRY}
ctrl_interface=DIR=/var/run/wpa_supplicant GROUP=netdev
update_config=1

network={
    ssid="${WIFI_SSID}"
    psk="${WIFI_PSK}"
}
EOF
    chmod 600 /etc/wpa_supplicant/wpa_supplicant.conf
    if command -v wpa_cli >/dev/null 2>&1; then
      wpa_cli -i wlan0 reconfigure || true
    fi
  fi
fi

for _ in $(seq 1 30); do
  if ping -c 1 -W 1 1.1.1.1 >/dev/null 2>&1; then
    break
  fi
  sleep 2
done

export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y --no-install-recommends ffmpeg v4l-utils nginx-light

install -D -m 644 /opt/shrimpcam/install/nginx/shrimpcam.conf /etc/nginx/sites-available/shrimpcam.conf
rm -f /etc/nginx/sites-enabled/default
ln -sf /etc/nginx/sites-available/shrimpcam.conf /etc/nginx/sites-enabled/shrimpcam.conf

systemctl daemon-reload
systemctl enable shrimpcam-api.service
systemctl restart shrimpcam-api.service
systemctl enable nginx
systemctl restart nginx

touch /opt/shrimpcam/.firstboot-complete
systemctl disable shrimpcam-firstboot.service
rm -f /etc/systemd/system/multi-user.target.wants/shrimpcam-firstboot.service
