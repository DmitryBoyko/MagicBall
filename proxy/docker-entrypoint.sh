#!/bin/sh
set -eu

mkdir -p /app/proxy/data

if [ "$(id -u)" = "0" ]; then
  chown -R appuser:appuser /app/proxy/data 2>/dev/null || true
  exec gosu appuser "$@"
fi

exec "$@"
