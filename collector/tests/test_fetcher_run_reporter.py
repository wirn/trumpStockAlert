from datetime import UTC, datetime
from io import BytesIO
from typing import Any
from urllib.error import HTTPError

import pytest

from collector.fetcher_run_reporter import FetcherRunReportError, FetcherRunReporter, _fmt


STARTED = datetime(2026, 5, 25, 10, 0, 0, tzinfo=UTC)
FINISHED = datetime(2026, 5, 25, 10, 0, 5, tzinfo=UTC)


class FakeReporter(FetcherRunReporter):
    """Subclass that captures payloads instead of making HTTP calls."""

    def __init__(self) -> None:
        super().__init__(base_url="http://api:8080", scheduler_api_key="test-key")
        self.posted_payloads: list[dict[str, Any]] = []
        self.raise_on_post: Exception | None = None

    def _post(self, payload: dict[str, Any]) -> None:
        if self.raise_on_post is not None:
            raise self.raise_on_post
        self.posted_payloads.append(payload)


def _make_http_error(code: int, body: bytes) -> HTTPError:
    return HTTPError(
        url="http://api:8080/api/collector/report-run",
        code=code,
        msg="error",
        hdrs={},  # type: ignore[arg-type]
        fp=BytesIO(body),
    )


# ---------------------------------------------------------------------------
# report_run payload shape
# ---------------------------------------------------------------------------


def test_report_run_sends_correct_payload() -> None:
    reporter = FakeReporter()

    reporter.report_run(
        started_at=STARTED,
        finished_at=FINISHED,
        success=True,
        fetched_count=10,
        inserted_count=3,
        duplicate_count=7,
        error_count=0,
        message="Collector completed.",
    )

    assert len(reporter.posted_payloads) == 1
    payload = reporter.posted_payloads[0]
    assert payload["startedAt"] == "2026-05-25T10:00:00Z"
    assert payload["finishedAt"] == "2026-05-25T10:00:05Z"
    assert payload["triggerType"] == "scheduler"
    assert payload["success"] is True
    assert payload["fetchedCount"] == 10
    assert payload["insertedCount"] == 3
    assert payload["duplicateCount"] == 7
    assert payload["errorCount"] == 0
    assert payload["message"] == "Collector completed."


def test_report_failure_sends_failure_payload() -> None:
    reporter = FakeReporter()

    reporter.report_failure(
        started_at=STARTED,
        message="Collector failed with an unhandled exception.",
    )

    assert len(reporter.posted_payloads) == 1
    payload = reporter.posted_payloads[0]
    assert payload["success"] is False
    assert payload["fetchedCount"] == 0
    assert payload["insertedCount"] == 0
    assert payload["duplicateCount"] == 0
    assert payload["errorCount"] == 1
    assert payload["message"] == "Collector failed with an unhandled exception."
    assert payload["triggerType"] == "scheduler"


def test_report_run_custom_trigger_type() -> None:
    reporter = FakeReporter()

    reporter.report_run(
        started_at=STARTED,
        finished_at=FINISHED,
        success=True,
        fetched_count=0,
        inserted_count=0,
        duplicate_count=0,
        error_count=0,
        message="ok",
        trigger_type="manual",
    )

    assert reporter.posted_payloads[0]["triggerType"] == "manual"


# ---------------------------------------------------------------------------
# Failures propagate as FetcherRunReportError
# ---------------------------------------------------------------------------


def test_report_run_raises_on_post_failure() -> None:
    reporter = FakeReporter()
    reporter.raise_on_post = RuntimeError("connection refused")

    with pytest.raises(RuntimeError, match="connection refused"):
        reporter.report_run(
            started_at=STARTED,
            finished_at=FINISHED,
            success=True,
            fetched_count=1,
            inserted_count=1,
            duplicate_count=0,
            error_count=0,
            message="ok",
        )


def test_report_failure_raises_on_post_failure() -> None:
    reporter = FakeReporter()
    reporter.raise_on_post = FetcherRunReportError("backend down")

    with pytest.raises(FetcherRunReportError, match="backend down"):
        reporter.report_failure(started_at=STARTED, message="Collector failed.")


# ---------------------------------------------------------------------------
# HTTP error handling in _post (via _open override)
# ---------------------------------------------------------------------------


def test_post_raises_fetcher_run_report_error_with_http_status_on_server_error() -> None:
    class ServerErrorReporter(FetcherRunReporter):
        def _open(self, *_args: Any, **_kwargs: Any) -> Any:
            raise _make_http_error(500, b'{"detail":"DB write failed"}')

    reporter = ServerErrorReporter(
        base_url="http://api:8080", scheduler_api_key="test-key"
    )

    with pytest.raises(FetcherRunReportError) as exc_info:
        reporter._post({"any": "payload"})

    assert "500" in str(exc_info.value)
    assert "DB write failed" in str(exc_info.value)


def test_post_raises_fetcher_run_report_error_on_unauthorized() -> None:
    class UnauthorizedReporter(FetcherRunReporter):
        def _open(self, *_args: Any, **_kwargs: Any) -> Any:
            raise _make_http_error(401, b'{"title":"Unauthorized"}')

    reporter = UnauthorizedReporter(
        base_url="http://api:8080", scheduler_api_key="wrong-key"
    )

    with pytest.raises(FetcherRunReportError) as exc_info:
        reporter._post({"any": "payload"})

    assert "401" in str(exc_info.value)


def test_post_raises_fetcher_run_report_error_on_connection_failure() -> None:
    class UnreachableReporter(FetcherRunReporter):
        def _open(self, *_args: Any, **_kwargs: Any) -> Any:
            raise OSError("Network unreachable")

    reporter = UnreachableReporter(
        base_url="http://api:8080", scheduler_api_key="test-key"
    )

    with pytest.raises(FetcherRunReportError, match="Failed to reach backend"):
        reporter._post({"any": "payload"})


# ---------------------------------------------------------------------------
# _fmt helper
# ---------------------------------------------------------------------------


def test_fmt_formats_utc_datetime() -> None:
    dt = datetime(2026, 5, 25, 10, 30, 45, tzinfo=UTC)
    assert _fmt(dt) == "2026-05-25T10:30:45Z"


def test_fmt_naive_datetime_treated_as_utc() -> None:
    dt = datetime(2026, 5, 25, 10, 30, 45)
    assert _fmt(dt) == "2026-05-25T10:30:45Z"
