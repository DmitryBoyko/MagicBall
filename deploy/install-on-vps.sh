#!/usr/bin/env bash
# First start on VPS (from /opt/magicalball). Manual deploy only — no auto-ssh.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

ENV_FILE="${ENV_FILE:-.env}"

if [[ ! -f "$ENV_FILE" ]]; then
  cp deploy/env.example "$ENV_FILE"
  echo "Created $ENV_FILE — edit GIGACHAT_CREDENTIALS and re-run this script."
  exit 1
fi

bash deploy/check-env.sh

echo "==> docker compose up"
docker compose --env-file "$ENV_FILE" up -d --build
docker compose --env-file "$ENV_FILE" ps

API_PORT="$(grep -E '^API_PORT=' "$ENV_FILE" | tail -n1 | cut -d= -f2- | tr -d '\r')"
API_PORT="${API_PORT:-18437}"
VPS_IP="$(grep -E '^VPS_IP=' "$ENV_FILE" | tail -n1 | cut -d= -f2- | tr -d '\r')"
VPS_IP="${VPS_IP:-147.45.173.26}"

echo "==> health (host port $API_PORT)"
for _ in $(seq 1 30); do
  if curl -fsS "http://127.0.0.1:${API_PORT}/health" >/dev/null 2>&1; then
    curl -fsS "http://127.0.0.1:${API_PORT}/health"
    echo
    echo "OK — MagicalBall proxy is up."
    echo "Public: http://${VPS_IP}:${API_PORT}/health"
    echo "Godot:  android_base_url = http://${VPS_IP}:${API_PORT}"
    exit 0
  fi
  sleep 2
done

echo "WARN: /health not ready yet. Check: docker compose logs -f proxy" >&2
exit 1
