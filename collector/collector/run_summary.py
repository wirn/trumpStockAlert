"""Summary of a single collector run."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime

from collector.models import NormalizedPost


@dataclass(frozen=True)
class CollectorRunSummary:
    new_posts: list[NormalizedPost]
    fetched_count: int
    saved_count: int
    already_existing_count: int
    failed_count: int
    started_at: datetime
    finished_at: datetime

    @property
    def success(self) -> bool:
        return self.failed_count == 0

    @property
    def message(self) -> str:
        if self.success:
            return "Collector completed."
        return f"Collector completed with {self.failed_count} failed post(s)."
