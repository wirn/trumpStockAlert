"""Tests for collector/main.py entry-point behaviour."""

from __future__ import annotations

import logging
from datetime import UTC, datetime
from typing import Any
from unittest.mock import MagicMock, patch

import pytest

from collector.fetcher_run_reporter import FetcherRunReportError, FetcherRunReporter
from collector.main import main
from collector.run_summary import CollectorRunSummary


STARTED = datetime(2026, 5, 25, 10, 0, 0, tzinfo=UTC)
FINISHED = datetime(2026, 5, 25, 10, 0, 5, tzinfo=UTC)


def _make_summary(**overrides: Any) -> CollectorRunSummary:
    defaults: dict[str, Any] = dict(
        new_posts=[],
        fetched_count=5,
        saved_count=2,
        already_existing_count=3,
        failed_count=0,
        started_at=STARTED,
        finished_at=FINISHED,
    )
    defaults.update(overrides)
    return CollectorRunSummary(**defaults)


@pytest.fixture()
def api_env(monkeypatch: pytest.MonkeyPatch) -> None:
    """Minimal environment variables for a valid API-mode run."""
    monkeypatch.setenv("COLLECTOR_STORE_MODE", "api")
    monkeypatch.setenv("SCHEDULER_API_KEY", "test-key")
    monkeypatch.setenv("TRUTH_POST_API_BASE_URL", "http://api:8080")
    monkeypatch.setenv("TRUTH_SOCIAL_USERNAME", "realDonaldTrump")
    monkeypatch.setenv("COLLECTOR_CLIENT_MODE", "truthbrush")
    monkeypatch.setenv("MAX_POSTS", "5")
    monkeypatch.setenv("LOOKBACK_MINUTES", "5")
    monkeypatch.setenv("OUTPUT_MODE", "console")


class _CallTrackingReporter:
    """Stand-in for FetcherRunReporter that records every call."""

    def __init__(self, base_url: str, scheduler_api_key: str, **_kwargs: Any) -> None:
        self.report_run_calls: list[dict[str, Any]] = []
        self.report_failure_calls: list[dict[str, Any]] = []
        self.raise_on_report_run: Exception | None = None
        self.raise_on_report_failure: Exception | None = None

    def report_run(self, **kwargs: Any) -> None:
        self.report_run_calls.append(kwargs)
        if self.raise_on_report_run is not None:
            raise self.raise_on_report_run

    def report_failure(self, **kwargs: Any) -> None:
        self.report_failure_calls.append(kwargs)
        if self.raise_on_report_failure is not None:
            raise self.raise_on_report_failure


def _patch_infrastructure(
    reporter_instance: _CallTrackingReporter,
    service_run_return: CollectorRunSummary | None = None,
    service_run_raises: Exception | None = None,
) -> "contextlib.AbstractContextManager[Any]":
    """Patch create_client, ApiTruthPostStore, CollectorService, and FetcherRunReporter."""
    import contextlib

    mock_service = MagicMock()
    if service_run_raises is not None:
        mock_service.run.side_effect = service_run_raises
    else:
        mock_service.run.return_value = (
            service_run_return if service_run_return is not None else _make_summary()
        )

    return contextlib.ExitStack()  # placeholder; we'll use nested patches below


# ---------------------------------------------------------------------------
# Missing SCHEDULER_API_KEY in API mode
# ---------------------------------------------------------------------------


def test_missing_scheduler_api_key_in_api_mode_fails_fast(
    api_env: None, monkeypatch: pytest.MonkeyPatch, caplog: pytest.LogCaptureFixture
) -> None:
    monkeypatch.setenv("SCHEDULER_API_KEY", "")

    with caplog.at_level(logging.ERROR):
        result = main([])

    assert result == 1
    assert "SCHEDULER_API_KEY" in caplog.text


def test_missing_scheduler_api_key_does_not_attempt_collect(
    api_env: None, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setenv("SCHEDULER_API_KEY", "")

    with patch("collector.main.CollectorService") as mock_svc:
        result = main([])

    assert result == 1
    mock_svc.assert_not_called()


# ---------------------------------------------------------------------------
# Successful run: exactly one report_run, zero report_failure
# ---------------------------------------------------------------------------


def test_successful_run_reports_exactly_once(api_env: None) -> None:
    tracker = _CallTrackingReporter(base_url="", scheduler_api_key="")
    summary = _make_summary(fetched_count=5, saved_count=2, already_existing_count=3, failed_count=0)

    with (
        patch("collector.main.FetcherRunReporter", return_value=tracker),
        patch("collector.main.CollectorService") as MockService,
        patch("collector.main.create_client", return_value=MagicMock()),
        patch("collector.main.ApiTruthPostStore", return_value=MagicMock()),
    ):
        MockService.return_value.run.return_value = summary
        result = main(["--skip-lookback"])

    assert result == 0
    assert len(tracker.report_run_calls) == 1
    assert len(tracker.report_failure_calls) == 0


def test_successful_run_passes_correct_counts_to_reporter(api_env: None) -> None:
    tracker = _CallTrackingReporter(base_url="", scheduler_api_key="")
    summary = _make_summary(fetched_count=10, saved_count=3, already_existing_count=6, failed_count=1)

    with (
        patch("collector.main.FetcherRunReporter", return_value=tracker),
        patch("collector.main.CollectorService") as MockService,
        patch("collector.main.create_client", return_value=MagicMock()),
        patch("collector.main.ApiTruthPostStore", return_value=MagicMock()),
    ):
        MockService.return_value.run.return_value = summary
        main(["--skip-lookback"])

    call = tracker.report_run_calls[0]
    assert call["fetched_count"] == 10
    assert call["inserted_count"] == 3
    assert call["duplicate_count"] == 6
    assert call["error_count"] == 1
    assert call["success"] is False  # failed_count > 0


# ---------------------------------------------------------------------------
# Collector failure: exactly one report_failure, zero report_run
# ---------------------------------------------------------------------------


def test_collector_failure_reports_failure_once(api_env: None) -> None:
    tracker = _CallTrackingReporter(base_url="", scheduler_api_key="")

    with (
        patch("collector.main.FetcherRunReporter", return_value=tracker),
        patch("collector.main.CollectorService") as MockService,
        patch("collector.main.create_client", return_value=MagicMock()),
        patch("collector.main.ApiTruthPostStore", return_value=MagicMock()),
    ):
        MockService.return_value.run.side_effect = RuntimeError("Truth Social blocked")
        result = main(["--skip-lookback"])

    assert result == 1
    assert len(tracker.report_failure_calls) == 1
    assert len(tracker.report_run_calls) == 0


def test_collector_failure_does_not_suppress_report_failure_error(
    api_env: None, caplog: pytest.LogCaptureFixture
) -> None:
    tracker = _CallTrackingReporter(base_url="", scheduler_api_key="")
    tracker.raise_on_report_failure = FetcherRunReportError("backend down")

    with (
        patch("collector.main.FetcherRunReporter", return_value=tracker),
        patch("collector.main.CollectorService") as MockService,
        patch("collector.main.create_client", return_value=MagicMock()),
        patch("collector.main.ApiTruthPostStore", return_value=MagicMock()),
        caplog.at_level(logging.ERROR),
    ):
        MockService.return_value.run.side_effect = RuntimeError("fetch failed")
        result = main(["--skip-lookback"])

    assert result == 1
    assert "backend down" in caplog.text


# ---------------------------------------------------------------------------
# Reporting failure after successful run exits non-zero
# ---------------------------------------------------------------------------


def test_report_run_failure_exits_nonzero(api_env: None) -> None:
    tracker = _CallTrackingReporter(base_url="", scheduler_api_key="")
    tracker.raise_on_report_run = FetcherRunReportError("500 from backend")

    with (
        patch("collector.main.FetcherRunReporter", return_value=tracker),
        patch("collector.main.CollectorService") as MockService,
        patch("collector.main.create_client", return_value=MagicMock()),
        patch("collector.main.ApiTruthPostStore", return_value=MagicMock()),
    ):
        MockService.return_value.run.return_value = _make_summary()
        result = main(["--skip-lookback"])

    assert result == 1
    assert len(tracker.report_run_calls) == 1   # was called (then raised)
    assert len(tracker.report_failure_calls) == 0  # not double-reported


def test_report_run_failure_does_not_call_report_failure(api_env: None) -> None:
    """Ensure a reporting failure after a successful run doesn't trigger report_failure."""
    tracker = _CallTrackingReporter(base_url="", scheduler_api_key="")
    tracker.raise_on_report_run = FetcherRunReportError("unreachable")

    with (
        patch("collector.main.FetcherRunReporter", return_value=tracker),
        patch("collector.main.CollectorService") as MockService,
        patch("collector.main.create_client", return_value=MagicMock()),
        patch("collector.main.ApiTruthPostStore", return_value=MagicMock()),
    ):
        MockService.return_value.run.return_value = _make_summary()
        main(["--skip-lookback"])

    assert len(tracker.report_failure_calls) == 0
