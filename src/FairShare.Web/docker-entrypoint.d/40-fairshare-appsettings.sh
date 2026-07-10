#!/bin/sh
# Writes the SPA's runtime config from the container environment so the API URL
# can be changed per-deployment without rebuilding the image. API_BASE_URL must
# be the browser-visible URL of FairShare.Api (never the compose-internal
# service name - the browser makes the calls, not this container).
set -e

if [ -n "${API_BASE_URL:-}" ]; then
    printf '{\n  "Api": {\n    "BaseUrl": "%s"\n  }\n}\n' "$API_BASE_URL" > /usr/share/nginx/html/appsettings.json
    echo "FairShare.Web: Api:BaseUrl set to $API_BASE_URL"
else
    echo "FairShare.Web: API_BASE_URL not set; using the appsettings.json baked into the image"
fi
