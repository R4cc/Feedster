#!/usr/bin/env bash
set -euo pipefail

image="${1:-feedster:ci}"
smoke_path="$(realpath "${2:-artifacts/container-smoke}")"
docker run --rm --entrypoint dotnet --mount "type=bind,source=$smoke_path,target=/ci,readonly" \
  "$image" /ci/Feedster.ContainerSmoke.dll /app

container="$(docker run -d -p 127.0.0.1:18080:8080 "$image")"
cleanup() {
  result=$?
  if [ "$result" -ne 0 ]; then docker logs "$container"; fi
  docker rm -f "$container" > /dev/null
}
trap cleanup EXIT

check_http() {
  for attempt in $(seq 1 60); do
    if curl --fail --silent http://127.0.0.1:18080/ > /dev/null; then return; fi
    sleep 1
  done
  return 1
}
check_http
for route in feeds/manage folders/manage settings css/app.css css/site.css js/alpine.min.js _framework/blazor.server.js; do
  curl --fail --silent "http://127.0.0.1:18080/$route" > /dev/null
done
headers="$(mktemp)"
curl --fail --silent --compressed -H 'Accept-Encoding: gzip' -D "$headers" \
  http://127.0.0.1:18080/css/app.css > /dev/null
grep -iq '^content-encoding: gzip' "$headers"
rm "$headers"
docker restart "$container" > /dev/null
check_http
echo 'Final container: native libraries, pages, assets, compression, and database restart passed.'
