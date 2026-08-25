#!/bin/sh
set -e

# ---------------------------------------------------------------------------
# SplitIt — Let's Encrypt automated certificate renewal loop.
#
# Runs inside the certbot-renewer container (profile: letsencrypt).
# Checks for existing certificates, performs initial issuance if missing,
# then enters a renewal loop every 12 hours.
#
# After each successful renewal, a sentinel file is written to the shared
# certbot_www volume. The proxy entrypoint watches for this file and
# reloads Nginx automatically — no docker exec required.
#
# Prerequisites:
#   - Proxy must be running and serving /.well-known/acme-challenge/ on :80
#   - Initial issuance requires the proxy to already be up (webroot mode)
#   - For first-time setup, see docs/HTTPS.md
# ---------------------------------------------------------------------------

DOMAIN="${DOMAIN:-localhost}"
ACME_EMAIL="${ACME_EMAIL:-admin@localhost}"
RENEWAL_INTERVAL="${RENEWAL_INTERVAL:-12h}"
SENTINEL_FILE="/var/www/certbot/.reload-trigger"

LE_DIR="/etc/letsencrypt/live/${DOMAIN}"

echo "=== SplitIt certbot-renewer ==="
echo "Domain: ${DOMAIN}"
echo "ACME email: ${ACME_EMAIL}"
echo "Renewal interval: ${RENEWAL_INTERVAL}"

# Wait for the proxy to be ready before proceeding.
echo "Waiting 15s for proxy to be ready..."
sleep 15

# --- Initial issuance (only if no certificate exists yet) ---
if [ ! -f "${LE_DIR}/fullchain.pem" ]; then
    echo "No existing certificate found for ${DOMAIN}."
    echo "Attempting initial issuance via webroot..."

    certbot certonly \
        --webroot \
        -w /var/www/certbot \
        --email "${ACME_EMAIL}" \
        -d "${DOMAIN}" \
        --agree-tos \
        --no-eff-email \
        --non-interactive \
        --keep-until-expiring

    if [ -f "${LE_DIR}/fullchain.pem" ]; then
        echo "Certificate issued successfully for ${DOMAIN}."
        touch "${SENTINEL_FILE}"
        chmod 666 "${SENTINEL_FILE}" 2>/dev/null || true
    else
        echo "WARNING: Initial issuance may have failed. Check logs above."
        echo "The renewal loop will retry on the next cycle."
    fi
else
    echo "Existing certificate found for ${DOMAIN}. Skipping initial issuance."
fi

# --- Renewal loop ---
echo "Starting certificate renewal loop (every ${RENEWAL_INTERVAL})..."

while true; do
    echo "[$(date)] Running certbot renew..."
    certbot renew \
        --deploy-hook "touch ${SENTINEL_FILE} && chmod 666 ${SENTINEL_FILE} 2>/dev/null || true" \
        --quiet

    echo "[$(date)] Renewal check complete. Sleeping ${RENEWAL_INTERVAL}..."
    sleep "${RENEWAL_INTERVAL}"
done
