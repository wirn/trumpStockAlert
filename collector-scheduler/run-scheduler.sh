#!/bin/sh
set -eu

api_url="${COLLECTOR_SCHEDULER_URL:-http://api:8080/api/collector/run}"
health_url="${COLLECTOR_SCHEDULER_HEALTH_URL:-http://api:8080/health}"
interval_seconds="${COLLECTOR_SCHEDULER_INTERVAL_SECONDS:-300}"
enabled="${COLLECTOR_SCHEDULER_ENABLED:-true}"

timestamp() {
  date -u +"%Y-%m-%dT%H:%M:%SZ"
}

log() {
  printf '%s %s\n' "$(timestamp)" "$*"
}

wait_for_api() {
  attempt=1
  log "Waiting for API health endpoint. HealthUrl=$health_url"

  while true; do
    error_file="$(mktemp)"
    curl_exit_code=0

    http_status="$(
      curl -sS \
        -o /dev/null \
        -w "%{http_code}" \
        "$health_url" \
        2>"$error_file"
    )" || curl_exit_code=$?

    error_body="$(cat "$error_file")"
    rm -f "$error_file"

    if [ "$curl_exit_code" -eq 0 ] && [ "$http_status" -ge 200 ] && [ "$http_status" -lt 300 ]; then
      log "API health check succeeded. HttpStatus=$http_status"
      return
    fi

    if [ "$curl_exit_code" -ne 0 ]; then
      log "API not ready. Attempt=$attempt ExitCode=$curl_exit_code HttpStatus=$http_status Error=$error_body"
    else
      log "API not ready. Attempt=$attempt HttpStatus=$http_status"
    fi

    attempt=$((attempt + 1))
    sleep 5
  done
}

if [ "$enabled" != "true" ] && [ "$enabled" != "1" ]; then
  log "Collector scheduler disabled. COLLECTOR_SCHEDULER_ENABLED=$enabled"
  while true; do
    sleep 3600
  done
fi

if [ -z "${SCHEDULER_API_KEY:-}" ]; then
  log "Warning: SCHEDULER_API_KEY is not configured; scheduled collector calls will be unauthorized."
fi

log "Collector scheduler started. Url=$api_url HealthUrl=$health_url IntervalSeconds=$interval_seconds"
wait_for_api

while true; do
  body_file="$(mktemp)"
  error_file="$(mktemp)"
  curl_exit_code=0

  http_status="$(
    curl -sS \
      -o "$body_file" \
      -w "%{http_code}" \
      -X POST \
      -H "Content-Type: application/json" \
      -H "X-TrumpStockAlert-Scheduler-Key: ${SCHEDULER_API_KEY:-}" \
      -H "X-TrumpStockAlert-Trigger-Type: Scheduler" \
      "$api_url" \
      2>"$error_file"
  )" || curl_exit_code=$?

  response_body="$(cat "$body_file")"
  error_body="$(cat "$error_file")"
  rm -f "$body_file" "$error_file"

  if [ "$curl_exit_code" -ne 0 ]; then
    log "Collector run request failed. ExitCode=$curl_exit_code Error=$error_body"
  else
    log "Collector run completed. HttpStatus=$http_status ResponseBody=$response_body"
  fi

  sleep "$interval_seconds"
done
