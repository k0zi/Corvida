#!/bin/sh
# Generates a self-signed certificate on first start so the HTTPS port works out of the box.
# Browsers will warn about it — put a real certificate in the corvida-web-certs volume, or
# terminate TLS at a proxy in front, for anything beyond local use.
set -e

CERT_DIR=/etc/nginx/certs
CERT="$CERT_DIR/corvida.crt"
KEY="$CERT_DIR/corvida.key"

mkdir -p "$CERT_DIR"

if [ -s "$CERT" ] && [ -s "$KEY" ]; then
    echo "corvida: using the existing certificate in $CERT_DIR"
    exit 0
fi

echo "corvida: generating a self-signed certificate in $CERT_DIR"
openssl req -x509 -nodes -newkey rsa:2048 -days 3650 \
    -keyout "$KEY" -out "$CERT" \
    -subj "/CN=${CORVIDA_WEB_HOST:-localhost}" \
    -addext "subjectAltName=DNS:${CORVIDA_WEB_HOST:-localhost},DNS:localhost,IP:127.0.0.1" \
    2>/dev/null

chmod 600 "$KEY"
