#Requires -Version 5.1
<#
.SYNOPSIS
    Generates a self-signed certificate and DH parameters for local HTTPS testing.

.DESCRIPTION
    The resulting files are stored in the Docker volume `splitit_nginx_certs`
    by starting a temporary container, so they are available to the proxy service
    on the next `docker compose up`.

    This script is intended for LOCAL TESTING ONLY. Production uses Let's Encrypt
    certificates mounted via the certbot volume.
#>
param(
    [string]$Domain = $env:DOMAIN,
    [switch]$SkipVolume
)

if ([string]::IsNullOrWhiteSpace($Domain)) {
    $Domain = 'localhost'
}

Write-Host "Generating local self-signed TLS material for domain: $Domain" -ForegroundColor Cyan

# Ensure Docker volume exists.
if (-not $SkipVolume) {
    docker volume create splitit_nginx_certs 2>$null | Out-Null
}

# Use the proxy image so we don't depend on host openssl.
docker run --rm `
    -v splitit_nginx_certs:/etc/nginx/ssl `
    -e DOMAIN=$Domain `
    nginx:1.27-alpine sh -c @'
set -e
mkdir -p /etc/nginx/ssl
if [ ! -f /etc/nginx/ssl/dhparam.pem ]; then
    echo "Generating 2048-bit DH parameters (one-time, ~30-60s)..."
    openssl dhparam -out /etc/nginx/ssl/dhparam.pem 2048
fi
if [ ! -f /etc/nginx/ssl/nginx.crt ] || [ ! -f /etc/nginx/ssl/nginx.key ]; then
    echo "Generating self-signed certificate..."
    openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
        -keyout /etc/nginx/ssl/nginx.key \
        -out /etc/nginx/ssl/nginx.crt \
        -subj "/CN=$DOMAIN" \
        -addext "subjectAltName=DNS:$DOMAIN,DNS:localhost,IP:127.0.0.1"
    chmod 600 /etc/nginx/ssl/nginx.key
    chmod 644 /etc/nginx/ssl/nginx.crt
fi
echo "TLS material ready."
'@

if ($LASTEXITCODE -ne 0) {
    throw "Failed to generate local TLS material."
}

Write-Host "Local TLS material is ready in Docker volume 'splitit_nginx_certs'." -ForegroundColor Green
