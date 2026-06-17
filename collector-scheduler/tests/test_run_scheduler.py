from __future__ import annotations

from pathlib import Path


SCRIPT = Path(__file__).resolve().parents[1] / "run-scheduler.sh"


def _script() -> str:
    return SCRIPT.read_text(encoding="utf-8")


def test_collector_success_triggers_analysis() -> None:
    script = _script()

    assert 'if [ "$collector_exit_code" -eq 0 ]; then' in script
    assert 'log "Collector run succeeded."' in script
    assert "run_analysis" in script


def test_successful_analysis_triggers_alerts() -> None:
    script = _script()

    assert "if run_analysis; then" in script
    assert "run_alerts" in script
    assert 'log "Alert run skipped because analysis did not complete successfully."' in script


def test_collector_failure_does_not_trigger_analysis_before_backoff() -> None:
    script = _script()
    failure_block = script.split('log "Collector run failed.', maxsplit=1)[1].split(
        "continue", maxsplit=1
    )[0]

    assert "run_analysis" not in failure_block
    assert "run_alerts" not in failure_block
    assert 'sleep "$backoff_seconds"' in failure_block


def test_analysis_disabled_skips_analysis_request() -> None:
    script = _script()

    assert 'analysis_enabled="${COLLECTOR_SCHEDULER_ANALYSIS_ENABLED:-true}"' in script
    assert 'if [ "$analysis_enabled" != "true" ] && [ "$analysis_enabled" != "1" ]; then' in script
    assert 'log "Analysis run skipped.' in script


def test_alerts_disabled_skips_alert_request() -> None:
    script = _script()

    assert 'alerts_enabled="${COLLECTOR_SCHEDULER_ALERTS_ENABLED:-true}"' in script
    assert 'alerts_url="${COLLECTOR_SCHEDULER_ALERTS_URL:-http://api:8080/api/alerts/run}"' in script
    assert 'if [ "$alerts_enabled" != "true" ] && [ "$alerts_enabled" != "1" ]; then' in script
    assert 'log "Alert run skipped.' in script


def test_analysis_http_failure_is_logged_without_exiting() -> None:
    script = _script()

    assert 'log "Analysis run failed. HttpStatus=$http_status' in script
    assert "ResponseBody=$response_body" in script
    assert "return 1" in script


def test_alert_http_failure_is_logged_without_exiting() -> None:
    script = _script()

    assert 'log "Alert run failed. HttpStatus=$http_status' in script
    assert "CreatedAlertCount=$created_count" in script
    assert "return 0" in script
