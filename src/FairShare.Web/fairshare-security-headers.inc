# Security headers for every response. The .inc extension keeps this file out of
# nginx's conf.d/*.conf auto-include glob; it is pulled in explicitly by nginx.conf -
# at server level AND inside every location that has its own add_header, because
# add_header in a nested location silently discards all inherited headers.
#
# CSP notes:
# - 'wasm-unsafe-eval' is required by the .NET (Blazor) WASM runtime.
# - style-src 'unsafe-inline' covers Blazor's and Bootstrap's inline style attributes.
# - connect-src 'self' is rewritten at container start by 50-fairshare-csp.sh to also
#   allow the cross-origin API (API_BASE_URL).
add_header Content-Security-Policy "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'" always;
add_header X-Content-Type-Options "nosniff" always;
add_header Referrer-Policy "strict-origin-when-cross-origin" always;
add_header Permissions-Policy "camera=(), microphone=(), geolocation=()" always;
add_header X-Frame-Options "DENY" always;
# HSTS reaches browsers via the reverse proxy's TLS termination (upstream response
# headers pass through); browsers ignore HSTS on plain-HTTP responses, so direct LAN
# access is unaffected. Deliberately conservative one-day max-age to start (#175) -
# raise toward 31536000 after a burn-in period. No includeSubDomains: the test
# subdomain's reachability must never hinge on this header.
add_header Strict-Transport-Security "max-age=86400" always;
# Empty (omitted) on prod; "noindex, nofollow" on the test host - see the map in
# nginx.conf. Keeps the nightly-reset duplicate site out of search indexes.
add_header X-Robots-Tag $fairshare_robots always;
