"""Collector orchestration."""

from __future__ import annotations

import json
import logging
from datetime import UTC, datetime

from collector.models import NormalizedPost
from collector.normalizer import PostNormalizer
from collector.truth_post_store import TruthPostStore
from collector.truth_social_client import TruthSocialClient

logger = logging.getLogger(__name__)


class CollectorService:
    def __init__(
        self,
        client: TruthSocialClient,
        normalizer: PostNormalizer,
        post_store: TruthPostStore,
        output_mode: str = "console",
    ) -> None:
        self.client = client
        self.normalizer = normalizer
        self.post_store = post_store
        self.output_mode = output_mode

    def run(
        self, max_posts: int, created_after: datetime | None = None
    ) -> list[NormalizedPost]:
        logger.info("Collector starting.")

        raw_posts = self.client.fetch_latest_posts(max_posts, created_after=created_after)
        logger.info("Fetched %s posts from Truthbrush.", len(raw_posts))

        normalized_posts = self.normalizer.normalize_many(raw_posts)
        relevant_posts = self._filter_by_created_at(normalized_posts, created_after)
        if created_after is not None:
            logger.info(
                "Kept %s posts created at or after %s.",
                len(relevant_posts),
                created_after.isoformat(),
            )

        new_posts = self.post_store.append_new_posts(relevant_posts)
        logger.info("Detected %s new posts.", len(new_posts))
        if not new_posts:
            logger.info("No new posts found; truth posts file was not rewritten.")

        self._output(new_posts)

        return new_posts

    def _filter_by_created_at(
        self, posts: list[NormalizedPost], created_after: datetime | None
    ) -> list[NormalizedPost]:
        if created_after is None:
            return posts

        created_after_utc = self._as_utc(created_after)
        relevant_posts: list[NormalizedPost] = []
        for post in posts:
            try:
                post_created_at = self._parse_created_at(post.createdAt)
            except ValueError as exc:
                logger.error(
                    "Skipping post %s because createdAt is invalid: %s",
                    post.externalId,
                    exc,
                )
                continue

            if post_created_at >= created_after_utc:
                relevant_posts.append(post)

        return relevant_posts

    def _parse_created_at(self, value: str) -> datetime:
        normalized = value.replace("Z", "+00:00")
        parsed = datetime.fromisoformat(normalized)
        return self._as_utc(parsed)

    def _as_utc(self, value: datetime) -> datetime:
        if value.tzinfo is None:
            return value.replace(tzinfo=UTC)
        return value.astimezone(UTC)

    def _output(self, posts: list[NormalizedPost]) -> None:
        if self.output_mode != "console":
            raise ValueError("Only console output is supported in step 1.")

        if not posts:
            return

        print(json.dumps([post.to_dict() for post in posts], indent=2))
