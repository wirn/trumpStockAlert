"""Local JSON storage that mimics the future truth_posts table."""

from __future__ import annotations

import json
import logging
from pathlib import Path
from typing import Any

from collector.models import NormalizedPost

logger = logging.getLogger(__name__)

PostKey = tuple[str, str]


class TruthPostStore:
    def __init__(self, path: Path) -> None:
        self.path = path

    def load_posts(self) -> list[dict[str, Any]]:
        if not self.path.exists():
            return []

        try:
            payload = json.loads(self.path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            raise ValueError(f"Truth posts file contains invalid JSON: {self.path}") from exc

        if not isinstance(payload, list):
            raise ValueError("Truth posts file must contain a JSON array.")

        posts: list[dict[str, Any]] = []
        for item in payload:
            if not isinstance(item, dict):
                raise ValueError("Truth posts file must contain only JSON objects.")
            posts.append(item)

        return posts

    def append_new_posts(self, posts: list[NormalizedPost]) -> list[NormalizedPost]:
        existing_posts = self.load_posts()
        existing_keys = self._build_existing_keys(existing_posts)

        new_posts: list[NormalizedPost] = []
        for post in posts:
            key = (post.source, post.externalId)
            if key in existing_keys:
                continue

            existing_keys.add(key)
            new_posts.append(post)

        if not new_posts:
            logger.info("No new posts to save.")
            return []

        updated_posts = existing_posts + [post.to_dict() for post in new_posts]
        self._save_posts(updated_posts)
        logger.info("Saved %s new posts to %s.", len(new_posts), self.path)
        return new_posts

    def _build_existing_keys(self, posts: list[dict[str, Any]]) -> set[PostKey]:
        keys: set[PostKey] = set()
        for post in posts:
            source = post.get("source")
            external_id = post.get("externalId")
            if isinstance(source, str) and isinstance(external_id, str):
                keys.add((source, external_id))
            else:
                logger.warning(
                    "Ignoring stored post without valid source/externalId: %r",
                    post,
                )
        return keys

    def _save_posts(self, posts: list[dict[str, Any]]) -> None:
        self.path.parent.mkdir(parents=True, exist_ok=True)
        serialized = json.dumps(posts, indent=2, ensure_ascii=False) + "\n"
        temp_path = self.path.with_suffix(f"{self.path.suffix}.tmp")

        try:
            temp_path.write_text(serialized, encoding="utf-8")
            temp_path.replace(self.path)
        except PermissionError:
            logger.warning(
                "Atomic truth posts update failed for %s; falling back to direct write.",
                self.path,
            )
            self.path.write_text(serialized, encoding="utf-8")
