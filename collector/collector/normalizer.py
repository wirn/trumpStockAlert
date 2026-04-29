"""Normalize raw Truthbrush posts into the Collector contract."""

from __future__ import annotations

import html
import logging
import re
from datetime import UTC, datetime
from typing import Any

from collector.models import NormalizedPost

logger = logging.getLogger(__name__)

HTML_TAG_PATTERN = re.compile(r"<[^>]+>")
WHITESPACE_PATTERN = re.compile(r"\s+")


class PostNormalizer:
    def __init__(self, author: str) -> None:
        self.author = author.lstrip("@")

    def normalize_many(self, raw_posts: list[dict[str, Any]]) -> list[NormalizedPost]:
        normalized: list[NormalizedPost] = []
        for raw_post in raw_posts:
            try:
                normalized.append(self.normalize(raw_post))
            except ValueError as exc:
                logger.error("Skipping malformed Truthbrush post: %s", exc)
        return normalized

    def normalize(self, raw_post: dict[str, Any]) -> NormalizedPost:
        external_id = self._required_string(raw_post, "id")
        created_at = self._required_string(raw_post, "created_at")
        content = self._resolve_content(raw_post)

        url = raw_post.get("url")
        if not isinstance(url, str) or not url.strip():
            url = f"https://truthsocial.com/@{self.author}/posts/{external_id}"

        return NormalizedPost(
            source="truthsocial",
            author=self.author,
            externalId=external_id,
            url=url,
            content=content,
            createdAt=created_at,
            collectedAt=datetime.now(UTC).isoformat(),
            raw=raw_post,
        )

    def _required_string(self, raw_post: dict[str, Any], key: str) -> str:
        value = raw_post.get(key)
        if not isinstance(value, str) or not value.strip():
            raise ValueError(f"missing or invalid `{key}`")
        return value

    def _clean_content(self, content: str) -> str:
        without_tags = HTML_TAG_PATTERN.sub(" ", content)
        decoded = html.unescape(without_tags)
        return WHITESPACE_PATTERN.sub(" ", decoded).strip()

    def _resolve_content(self, raw_post: dict[str, Any]) -> str:
        candidates = [
            self._clean_optional_string(raw_post.get("content")),
            self._clean_optional_string(raw_post.get("text")),
            self._clean_optional_string(raw_post.get("title")),
            self._content_from_card(raw_post.get("card")),
            self._content_from_embedded_post(raw_post.get("quote")),
            self._content_from_embedded_post(raw_post.get("reblog")),
        ]

        for candidate in candidates:
            if candidate:
                return candidate

        return "[No text content]"

    def _clean_optional_string(self, value: Any) -> str | None:
        if not isinstance(value, str):
            return None

        cleaned = self._clean_content(value)
        return cleaned or None

    def _content_from_card(self, value: Any) -> str | None:
        if not isinstance(value, dict):
            return None

        candidates = [
            self._clean_optional_string(value.get("title")),
            self._clean_optional_string(value.get("description")),
        ]
        return next((candidate for candidate in candidates if candidate), None)

    def _content_from_embedded_post(self, value: Any) -> str | None:
        if not isinstance(value, dict):
            return None

        candidates = [
            self._clean_optional_string(value.get("content")),
            self._clean_optional_string(value.get("text")),
            self._clean_optional_string(value.get("title")),
            self._content_from_card(value.get("card")),
        ]
        return next((candidate for candidate in candidates if candidate), None)
