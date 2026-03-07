#!/usr/bin/env bash
set -euo pipefail

CERT_DIR="${CERT_DIR:-.aspnet/https}"
CERT_NAME="${CERT_NAME:-hashprocessing.api.pfx}"
CERT_PASSWORD="${HTTPS_CERT_PASSWORD:-devpassword}"
CERT_PATH="$CERT_DIR/$CERT_NAME"

mkdir -p "$CERT_DIR"

if [[ -f "$CERT_PATH" ]]; then
  echo "HTTPS certificate already exists at $CERT_PATH"
  exit 0
fi

echo "Generating ASP.NET Core development certificate..."
dotnet dev-certs https > /dev/null

echo "Exporting certificate to $CERT_PATH"
dotnet dev-certs https -ep "$CERT_PATH" -p "$CERT_PASSWORD" > /dev/null

echo "Trusting the development certificate..."
dotnet dev-certs https --trust

echo "Done. Certificate exported and trusted for Docker Compose."
