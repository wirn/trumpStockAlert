#!/bin/sh
set -eu

health_url="${COLLECTOR_SCHEDULER_HEALTH_URL:-http://api:8080/health}"
interval_seconds="${COLLECTOR_SCHEDULER_INTERVAL_SECONDS:-300}"
jitter_seconds="${COLLECTOR_SCHEDULER_JITTER_SECONDS:-120}"
backoff_seconds="${COLLECTOR_SCHEDULER_BACKOFF_SECONDS:-900}"
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
      log "API not ready. Attempt=$attempt ExitCode=$curl_exit_code Error=$error_body"
    else
      log "API not ready. Attempt=$attempt HttpStatus=$http_status"
    fi

    attempt=$((attempt + 1))
    sleep 5
  done
}

random_jitter() {
  if [ "$jitter_seconds" -le 0 ]; then
    printf '0'
    return
  fi

  od -An -N4 -tu4 /dev/urandom | awk -v max="$jitter_seconds" '{ print $1 % (max + 1) }'
}

if [ "$enabled" != "true" ] && [ "$enabled" != "1" ]; then
  log "Collector scheduler disabled. COLLECTOR_SCHEDULER_ENABLED=$enabled"
  while true; do
    sleep 3600
  done
fi

log "Collector scheduler started. IntervalSeconds=$interval_seconds JitterSeconds=$jitter_seconds BackoffSeconds=$backoff_seconds HealthUrl=$health_url"
wait_for_api

while true; do
  log "Starting collector run via docker compose."

  collector_exit_code=0
  docker compose run --rm --build collector || collector_exit_code=$?

  if [ "$collector_exit_code" -eq 0 ]; then
    log "Collector run succeeded."
  else
    log "Collector run failed. ExitCode=$collector_exit_code Applying backoff. BackoffSeconds=$backoff_seconds"
    sleep "$backoff_seconds"
    continue
  fi

  jitter="$(random_jitter)"
  sleep_seconds=$((interval_seconds + jitter))
  log "Sleeping before next collector run. BaseIntervalSeconds=$interval_seconds JitterSeconds=$jitter SleepSeconds=$sleep_seconds"
  sleep "$sleep_seconds"
done
