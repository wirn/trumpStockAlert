"""Shared result model for collector post persistence."""

from __future__ import annotations

from dataclasses import dataclass

from collector.models import NormalizedPost


@dataclass(frozen=True)
class SavePostsResult:
    saved_posts: list[NormalizedPost]
    already_existing_count: int
    failed_count: int = 0

    @property
    def saved_count(self) -> int:
        return len(self.saved_posts)
