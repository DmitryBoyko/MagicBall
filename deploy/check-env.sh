#!/usr/bin/env bash
# Validate /opt/magicalball/.env before first deploy or reload.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ENV_FILE="${ENV_FILE:-$ROOT/.env}"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR: missing $ENV_FILE — run: cp deploy/env.example .env && nano .env" >&2
  exit 1
fi

# shellcheck disable=SC1090
set -a
source <(grep -E '^(VPS_IP|API_PORT|GIGACHAT_CREDENTIALS)=' "$ENV_FILE" | sed 's/\r$//')
set +a

API_PORT="${API_PORT:-18437}"
VPS_IP="${VPS_IP:-147.45.173.26}"

if [[ -z "${GIGACHAT_CREDENTIALS:-}" ]]; then
  echo "ERROR: GIGACHAT_CREDENTIALS is empty in $ENV_FILE" >&2
  exit 1
fi

if ! [[ "$API_PORT" =~ ^[0-9]+$ ]] || (( API_PORT < 1024 || API_PORT > 65535 )); then
  echo "ERROR: API_PORT must be 1024-65535, got: $API_PORT" >&2
  exit 1
fi

echo "OK — env looks valid"
echo "  VPS_IP=$VPS_IP"
echo "  API_PORT=$API_PORT"
echo "  Public URL: http://${VPS_IP}:${API_PORT}"
echo "  Godot android_base_url: http://${VPS_IP}:${API_PORT}"
