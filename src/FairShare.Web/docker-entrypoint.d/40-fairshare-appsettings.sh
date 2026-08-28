#!/bin/sh
# Writes the SPA's runtime config from the container environment so the API URL
# can be changed per-deployment without rebuilding the image. API_BASE_URL must
# be the browser-visible URL of FairShare.Api (never the compose-internal
# service name - the browser makes the calls, not this container).
set -e

if [ -n "${API_BASE_URL:-}" ]; then
    # JSON-escape backslashes and double quotes so a malformed .env value can't
    # produce invalid JSON that silently breaks the SPA's config load.
    escaped=$(printf '%s' "$API_BASE_URL" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g')
    printf '{\n  "Api": {\n    "BaseUrl": "%s"\n  }\n}\n' "$escaped" > /usr/share/nginx/html/appsettings.json
    # The publish ships precompressed siblings of the ORIGINAL file, and gzip_static
    # prefers them: browsers sending Accept-Encoding got the stale publish-time config
    # while curl-without-encoding got the fresh one (exactly how the 4.4.0 regression
    # hid - the SPA fell back to its own origin and 404'd every API call). Deleting
    # the siblings makes nginx serve the rewritten file to everyone.
    rm -f /usr/share/nginx/html/appsettings.json.gz /usr/share/nginx/html/appsettings.json.br
    echo "FairShare.Web: Api:BaseUrl set to $API_BASE_URL (stale precompressed siblings removed)"
else
    echo "FairShare.Web: API_BASE_URL not set; using the appsettings.json baked into the image"
fi
