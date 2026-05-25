"""Reports collector run results to the backend fetcher_runs table."""

from __future__ import annotations

import json
import logging
import urllib.error
import urllib.request
from datetime import UTC, datetime
from typing import Any

logger = logging.getLogger(__name__)


class FetcherRunReportError(Exception):
    """Raised when reporting a fetcher run to the backend fails."""


class FetcherRunReporter:
    def __init__(
        self,
        base_url: str,
        scheduler_api_key: str,
        timeout_seconds: int = 10,
    ) -> None:
        self.base_url = base_url.rstrip("/")
        self.scheduler_api_key = scheduler_api_key
        self.timeout_seconds = timeout_seconds
        self.endpoint_url = f"{self.base_url}/api/collector/report-run"

    def report_run(
        self,
        *,
        started_at: datetime,
        finished_at: datetime,
        success: bool,
        fetched_count: int,
        inserted_count: int,
        duplicate_count: int,
        error_count: int,
        message: str,
        trigger_type: str = "scheduler",
    ) -> None:
        """Post run metrics to the backend. Raises FetcherRunReportError on any failure."""
        payload: dict[str, Any] = {
            "startedAt": _fmt(started_at),
            "finishedAt": _fmt(finished_at),
            "triggerType": trigger_type,
            "success": success,
            "fetchedCount": fetched_count,
            "insertedCount": inserted_count,
            "duplicateCount": duplicate_count,
            "errorCount": error_count,
            "message": message,
        }
        self._post(payload)

    def report_failure(
        self,
        *,
        started_at: datetime,
        message: str,
        trigger_type: str = "scheduler",
    ) -> None:
        """Post a failure record to the backend. Raises FetcherRunReportError on any failure."""
        finished_at = datetime.now(UTC)
        self.report_run(
            started_at=started_at,
            finished_at=finished_at,
            success=False,
            fetched_count=0,
            inserted_count=0,
            duplicate_count=0,
            error_count=1,
            message=message,
            trigger_type=trigger_type,
        )

    def _post(self, payload: dict[str, Any]) -> None:
        body = json.dumps(payload).encode("utf-8")
        req = urllib.request.Request(
            self.endpoint_url,
            data=body,
            method="POST",
            headers={
                "Content-Type": "application/json",
                "X-TrumpStockAlert-Scheduler-Key": self.scheduler_api_key,
            },
        )
        try:
            with self._open(req, self.timeout_seconds) as resp:
                logger.info(
                    "FetcherRun reported to backend. StatusCode=%s.", resp.status
                )
        except urllib.error.HTTPError as exc:
            try:
                response_body = exc.read().decode("utf-8", errors="replace")
            except Exception:
                response_body = "<unreadable>"
            raise FetcherRunReportError(
                f"Backend returned HTTP {exc.code} from {self.endpoint_url}: "
                f"{response_body[:500]}"
            ) from exc
        except Exception as exc:
            raise FetcherRunReportError(
                f"Failed to reach backend at {self.endpoint_url}: {exc}"
            ) from exc

    def _open(self, request: urllib.request.Request, timeout: int) -> Any:
        return urllib.request.urlopen(request, timeout=timeout)


def _fmt(dt: datetime) -> str:
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=UTC)
    return dt.astimezone(UTC).isoformat().replace("+00:00", "Z")
