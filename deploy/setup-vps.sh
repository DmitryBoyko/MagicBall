#!/usr/bin/env bash
# One-shot VPS setup: .env + GigaChat key + firewall + docker compose + health check.
# Run from /opt/magicalball after WinSCP upload:
#   chmod +x deploy/*.sh && bash deploy/setup-vps.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
ENV_FILE="$ROOT/.env"
TRUETARO_ENV="/opt/truetaro/.env"

set_env_var() {
  local key="$1"
  local value="$2"
  if grep -q "^${key}=" "$ENV_FILE" 2>/dev/null; then
    sed -i "s|^${key}=.*|${key}=${value}|" "$ENV_FILE"
  else
    echo "${key}=${value}" >> "$ENV_FILE"
  fi
}

read_env_var() {
  local key="$1"
  local file="$2"
  grep -E "^${key}=" "$file" 2>/dev/null | tail -n1 | cut -d= -f2- | tr -d '\r' || true
}

echo "==> MagicalBall VPS setup"
echo "    dir: $ROOT"

if [[ ! -f "$ENV_FILE" ]]; then
  cp deploy/env.example "$ENV_FILE"
  echo "    created .env from deploy/env.example"
fi

# Defaults (already in env.example, but enforce if someone stripped them)
VPS_IP="$(read_env_var VPS_IP "$ENV_FILE")"
API_PORT="$(read_env_var API_PORT "$ENV_FILE")"
CRED="$(read_env_var GIGACHAT_CREDENTIALS "$ENV_FILE")"
VPS_IP="${VPS_IP:-147.45.173.26}"
API_PORT="${API_PORT:-18437}"

set_env_var VPS_IP "$VPS_IP"
set_env_var API_PORT "$API_PORT"

if [[ -z "$CRED" && -f "$TRUETARO_ENV" ]]; then
  CRED="$(read_env_var GIGACHAT_CREDENTIALS "$TRUETARO_ENV")"
  if [[ -n "$CRED" ]]; then
    set_env_var GIGACHAT_CREDENTIALS "$CRED"
    echo "    GIGACHAT_CREDENTIALS copied from $TRUETARO_ENV"
  fi
fi

CRED="$(read_env_var GIGACHAT_CREDENTIALS "$ENV_FILE")"
if [[ -z "$CRED" ]]; then
  echo ""
  echo "ERROR: GIGACHAT_CREDENTIALS is empty." >&2
  echo "Fix one of:" >&2
  echo "  1) Put key in $TRUETARO_ENV (then re-run this script)" >&2
  echo "  2) nano $ENV_FILE  ->  GIGACHAT_CREDENTIALS=your_key" >&2
  echo "  3) export GIGACHAT_CREDENTIALS=your_key && bash deploy/setup-vps.sh" >&2
  exit 1
fi

if [[ -n "${GIGACHAT_CREDENTIALS:-}" ]]; then
  set_env_var GIGACHAT_CREDENTIALS "$GIGACHAT_CREDENTIALS"
  echo "    GIGACHAT_CREDENTIALS from environment"
fi

bash deploy/check-env.sh

if command -v ufw >/dev/null 2>&1 && ufw status 2>/dev/null | grep -q "Status: active"; then
  echo "==> ufw allow ${API_PORT}/tcp"
  ufw allow "${API_PORT}/tcp" || true
fi

bash deploy/install-on-vps.sh

echo ""
echo "==> external check"
curl -fsS "http://${VPS_IP}:${API_PORT}/health"
echo ""
echo ""
echo "DONE."
echo "  Health:  http://${VPS_IP}:${API_PORT}/health"
echo "  Oracle:  http://${VPS_IP}:${API_PORT}/api/v1/oracle"
echo "  Godot:   android_base_url = http://${VPS_IP}:${API_PORT}"
