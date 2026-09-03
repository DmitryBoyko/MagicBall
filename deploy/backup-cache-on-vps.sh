#!/usr/bin/env bash
# Copy semantic_cache.json from the Docker volume into deploy/backups/.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

CONTAINER="${CONTAINER:-magicalball-proxy}"
BACKUP_DIR="$ROOT/deploy/backups"
STAMP="$(date +%Y%m%d-%H%M)"
OUT="$BACKUP_DIR/cache-${STAMP}.json"

mkdir -p "$BACKUP_DIR"

if ! docker ps --format '{{.Names}}' | grep -qx "$CONTAINER"; then
  echo "ERROR: container $CONTAINER is not running. Run install-on-vps.sh first." >&2
  exit 1
fi

docker cp "${CONTAINER}:/app/proxy/data/semantic_cache.json" "$OUT"
echo "OK — backup: $OUT ($(wc -c < "$OUT") bytes)"
