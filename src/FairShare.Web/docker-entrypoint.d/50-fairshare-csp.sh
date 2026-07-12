#!/bin/sh
# The SPA and API run on different origins, so the CSP's connect-src must allow the
# browser-visible API origin. Injected at container start (same pattern as
# 40-fairshare-appsettings.sh) so the CSP tracks API_BASE_URL without an image rebuild.
set -e

HEADERS_FILE=/etc/nginx/conf.d/fairshare-security-headers.inc

if [ -n "${API_BASE_URL:-}" ]; then
    # Strip any trailing slash: CSP source expressions are origins, not URLs.
    origin=$(printf '%s' "$API_BASE_URL" | sed 's:/*$::')
    # Pipe delimiter because the origin contains '://'.
    sed -i "s|connect-src 'self'|connect-src 'self' ${origin}|" "$HEADERS_FILE"
    echo "FairShare.Web: CSP connect-src extended with $origin"
else
    echo "FairShare.Web: API_BASE_URL not set; CSP connect-src stays same-origin"
fi
