#!/bin/sh
# The SPA and API run on different origins, so the CSP's connect-src must allow the
# browser-visible API origin. Injected at container start (same pattern as
# 40-fairshare-appsettings.sh) so the CSP tracks API_BASE_URL without an image rebuild.
set -e

HEADERS_FILE=/etc/nginx/conf.d/fairshare-security-headers.inc

if [ -n "${API_BASE_URL:-}" ]; then
    # Reduce to an origin (scheme://host[:port]) - CSP source expressions are origins,
    # not URLs, so any path/query on API_BASE_URL must be dropped.
    origin=$(printf '%s' "$API_BASE_URL" | sed -E 's|^([A-Za-z][A-Za-z0-9+.-]*://[^/]+).*|\1|')
    # Escape sed-replacement metacharacters so a hostile/odd value can't mangle the file.
    escaped=$(printf '%s' "$origin" | sed 's/[&\\|]/\\&/g')
    # Replace the whole connect-src clause (not just append after 'self') so re-running
    # on container restart is idempotent and an old origin never lingers.
    sed -i "s|connect-src [^;]*|connect-src 'self' ${escaped}|" "$HEADERS_FILE"
    echo "FairShare.Web: CSP connect-src set to 'self' $origin"
else
    echo "FairShare.Web: API_BASE_URL not set; CSP connect-src stays same-origin"
fi
