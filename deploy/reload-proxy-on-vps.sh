#!/usr/bin/env bash
# Rebuild/recreate API after code upload. Does not touch magicalball_cache volume.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

ENV_FILE="${ENV_FILE:-.env}"
echo "NEVER run: docker compose down -v"
docker compose --env-file "$ENV_FILE" up -d --build --no-deps --force-recreate proxy
docker compose --env-file "$ENV_FILE" ps

API_PORT="$(grep -E '^API_PORT=' "$ENV_FILE" 2>/dev/null | tail -n1 | cut -d= -f2- | tr -d '\r')"
API_PORT="${API_PORT:-18437}"
VPS_IP="$(grep -E '^VPS_IP=' "$ENV_FILE" 2>/dev/null | tail -n1 | cut -d= -f2- | tr -d '\r')"
VPS_IP="${VPS_IP:-147.45.173.26}"

curl -fsS "http://127.0.0.1:${API_PORT}/health" || true
echo
echo "Public URL for Godot: http://${VPS_IP}:${API_PORT}"
