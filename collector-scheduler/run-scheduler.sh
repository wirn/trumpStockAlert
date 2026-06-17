#!/bin/sh
set -eu

health_url="${COLLECTOR_SCHEDULER_HEALTH_URL:-http://api:8080/health}"
interval_seconds="${COLLECTOR_SCHEDULER_INTERVAL_SECONDS:-300}"
jitter_seconds="${COLLECTOR_SCHEDULER_JITTER_SECONDS:-120}"
backoff_seconds="${COLLECTOR_SCHEDULER_BACKOFF_SECONDS:-900}"
enabled="${COLLECTOR_SCHEDULER_ENABLED:-true}"
analysis_enabled="${COLLECTOR_SCHEDULER_ANALYSIS_ENABLED:-true}"
analysis_url="${COLLECTOR_SCHEDULER_ANALYSIS_URL:-http://api:8080/api/analyses/run}"
alerts_enabled="${COLLECTOR_SCHEDULER_ALERTS_ENABLED:-true}"
alerts_url="${COLLECTOR_SCHEDULER_ALERTS_URL:-http://api:8080/api/alerts/run}"
run_once="${COLLECTOR_SCHEDULER_RUN_ONCE:-false}"

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

json_field() {
  printf '%s' "$1" | jq -r --arg key "$2" '.[$key] // ""' 2>/dev/null || true
}

run_analysis() {
  if [ "$analysis_enabled" != "true" ] && [ "$analysis_enabled" != "1" ]; then
    log "Analysis run skipped. COLLECTOR_SCHEDULER_ANALYSIS_ENABLED=$analysis_enabled"
    return 1
  fi

  response_file="$(mktemp)"
  error_file="$(mktemp)"
  curl_exit_code=0

  http_status="$(
    curl -sS \
      -X POST \
      -H "X-TrumpStockAlert-Scheduler-Key: ${SCHEDULER_API_KEY:-}" \
      -o "$response_file" \
      -w "%{http_code}" \
      "$analysis_url" \
      2>"$error_file"
  )" || curl_exit_code=$?

  response_body="$(cat "$response_file")"
  error_body="$(cat "$error_file")"
  rm -f "$response_file" "$error_file"

  if [ "$curl_exit_code" -ne 0 ]; then
    log "Analysis run request failed. ExitCode=$curl_exit_code Error=$error_body"
    return 1
  fi

  analyzed_count="$(json_field "$response_body" "analyzedCount")"
  skipped_count="$(json_field "$response_body" "skippedCount")"
  error_count="$(json_field "$response_body" "errorCount")"
  message="$(json_field "$response_body" "message")"

  if [ "$http_status" -ge 200 ] && [ "$http_status" -lt 300 ]; then
    log "Analysis run completed. HttpStatus=$http_status AnalyzedCount=$analyzed_count SkippedCount=$skipped_count ErrorCount=$error_count Message=$message"
    return 0
  fi

  log "Analysis run failed. HttpStatus=$http_status AnalyzedCount=$analyzed_count SkippedCount=$skipped_count ErrorCount=$error_count Message=$message ResponseBody=$response_body"
  return 1
}

run_alerts() {
  if [ "$alerts_enabled" != "true" ] && [ "$alerts_enabled" != "1" ]; then
    log "Alert run skipped. COLLECTOR_SCHEDULER_ALERTS_ENABLED=$alerts_enabled"
    return 0
  fi

  response_file="$(mktemp)"
  error_file="$(mktemp)"
  curl_exit_code=0

  http_status="$(
    curl -sS \
      -X POST \
      -H "X-TrumpStockAlert-Scheduler-Key: ${SCHEDULER_API_KEY:-}" \
      -o "$response_file" \
      -w "%{http_code}" \
      "$alerts_url" \
      2>"$error_file"
  )" || curl_exit_code=$?

  response_body="$(cat "$response_file")"
  error_body="$(cat "$error_file")"
  rm -f "$response_file" "$error_file"

  if [ "$curl_exit_code" -ne 0 ]; then
    log "Alert run request failed. ExitCode=$curl_exit_code Error=$error_body"
    return 0
  fi

  evaluated_count="$(json_field "$response_body" "evaluatedAnalysisCount")"
  eligible_count="$(json_field "$response_body" "eligibleAnalysisCount")"
  created_count="$(json_field "$response_body" "createdAlertCount")"
  duplicate_count="$(json_field "$response_body" "duplicateCount")"
  sent_count="$(json_field "$response_body" "sentCount")"
  failed_count="$(json_field "$response_body" "failedCount")"
  message="$(json_field "$response_body" "message")"

  if [ "$http_status" -ge 200 ] && [ "$http_status" -lt 300 ]; then
    log "Alert run completed. HttpStatus=$http_status EvaluatedAnalysisCount=$evaluated_count EligibleAnalysisCount=$eligible_count CreatedAlertCount=$created_count DuplicateCount=$duplicate_count SentCount=$sent_count FailedCount=$failed_count Message=$message"
    return 0
  fi

  log "Alert run failed. HttpStatus=$http_status EvaluatedAnalysisCount=$evaluated_count EligibleAnalysisCount=$eligible_count CreatedAlertCount=$created_count DuplicateCount=$duplicate_count SentCount=$sent_count FailedCount=$failed_count Message=$message ResponseBody=$response_body"
  return 0
}

if [ "$enabled" != "true" ] && [ "$enabled" != "1" ]; then
  log "Collector scheduler disabled. COLLECTOR_SCHEDULER_ENABLED=$enabled"
  while true; do
    sleep 3600
  done
fi

log "Collector scheduler started. IntervalSeconds=$interval_seconds JitterSeconds=$jitter_seconds BackoffSeconds=$backoff_seconds HealthUrl=$health_url AnalysisEnabled=$analysis_enabled AnalysisUrl=$analysis_url AlertsEnabled=$alerts_enabled AlertsUrl=$alerts_url"
wait_for_api

while true; do
  log "Starting collector run via docker compose."

  collector_exit_code=0
  docker compose run --rm --build collector || collector_exit_code=$?

  if [ "$collector_exit_code" -eq 0 ]; then
    log "Collector run succeeded."
    if run_analysis; then
      run_alerts
    else
      log "Alert run skipped because analysis did not complete successfully."
    fi
  else
    log "Collector run failed. ExitCode=$collector_exit_code Applying backoff. BackoffSeconds=$backoff_seconds"
    sleep "$backoff_seconds"
    if [ "$run_once" = "true" ] || [ "$run_once" = "1" ]; then
      log "Collector scheduler run-once mode completed after collector failure."
      exit "$collector_exit_code"
    fi
    continue
  fi

  if [ "$run_once" = "true" ] || [ "$run_once" = "1" ]; then
    log "Collector scheduler run-once mode completed."
    exit 0
  fi

  jitter="$(random_jitter)"
  sleep_seconds=$((interval_seconds + jitter))
  log "Sleeping before next collector run. BaseIntervalSeconds=$interval_seconds JitterSeconds=$jitter SleepSeconds=$sleep_seconds"
  sleep "$sleep_seconds"
done
