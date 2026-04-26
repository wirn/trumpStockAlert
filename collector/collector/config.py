"""Environment-based configuration for the Collector."""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class CollectorConfig:
    truth_social_username: str = "realDonaldTrump"
    max_posts: int = 10
    lookback_minutes: int = 5
    truth_posts_file_path: Path = Path("./data/truth-posts.json")
    output_mode: str = "console"

    @classmethod
    def from_env(cls) -> "CollectorConfig":
        max_posts_raw = os.getenv("MAX_POSTS", "10")
        try:
            max_posts = int(max_posts_raw)
        except ValueError as exc:
            raise ValueError("MAX_POSTS must be an integer.") from exc

        if max_posts < 1:
            raise ValueError("MAX_POSTS must be at least 1.")

        lookback_minutes_raw = os.getenv("LOOKBACK_MINUTES", "5")
        try:
            lookback_minutes = int(lookback_minutes_raw)
        except ValueError as exc:
            raise ValueError("LOOKBACK_MINUTES must be an integer.") from exc

        if lookback_minutes < 1:
            raise ValueError("LOOKBACK_MINUTES must be at least 1.")

        output_mode = os.getenv("OUTPUT_MODE", "console").strip().lower()
        if output_mode != "console":
            raise ValueError("Only OUTPUT_MODE=console is supported in step 1.")

        username = os.getenv("TRUTH_SOCIAL_USERNAME", "realDonaldTrump").strip()
        if not username:
            raise ValueError("TRUTH_SOCIAL_USERNAME cannot be empty.")

        return cls(
            truth_social_username=username.lstrip("@"),
            max_posts=max_posts,
            lookback_minutes=lookback_minutes,
            truth_posts_file_path=Path(
                os.getenv("TRUTH_POSTS_FILE_PATH", "./data/truth-posts.json")
            ),
            output_mode=output_mode,
        )
