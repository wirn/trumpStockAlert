"""Truth Social access via Truthbrush."""

from __future__ import annotations

import json
import logging
import shutil
import subprocess
from collections.abc import Iterable
from datetime import UTC, datetime
from typing import Any

logger = logging.getLogger(__name__)


class TruthSocialClientError(RuntimeError):
    """Raised when Truthbrush cannot fetch posts."""


class TruthSocialClient:
    """Fetches recent Truth Social posts using Truthbrush."""

    def __init__(self, username: str) -> None:
        self.username = username.lstrip("@")

    def fetch_latest_posts(
        self, max_posts: int, created_after: datetime | None = None
    ) -> list[dict[str, Any]]:
        try:
            return self._fetch_with_python_api(max_posts, created_after)
        except ImportError:
            logger.warning("Truthbrush Python package import failed; trying CLI fallback.")
            return self._fetch_with_cli(max_posts, created_after)
        except Exception as exc:
            raise TruthSocialClientError(
                f"Truthbrush failed while fetching posts for @{self.username}: {exc}"
            ) from exc

    def _fetch_with_python_api(
        self, max_posts: int, created_after: datetime | None = None
    ) -> list[dict[str, Any]]:
        from truthbrush.api import Api  # type: ignore[import-not-found]

        api = Api(require_auth=False)
        posts: list[dict[str, Any]] = []
        for raw_post in api.pull_statuses(self.username, created_after=created_after):
            if isinstance(raw_post, dict):
                if created_after is not None:
                    is_relevant = self._is_on_or_after(raw_post, created_after)
                    if is_relevant is False:
                        break
                    if is_relevant is None:
                        continue
                posts.append(raw_post)
            else:
                logger.warning("Skipping non-object Truthbrush post: %r", raw_post)

            if len(posts) >= max_posts:
                break

        return posts

    def _is_on_or_after(
        self, raw_post: dict[str, Any], created_after: datetime
    ) -> bool | None:
        created_at = raw_post.get("created_at")
        if not isinstance(created_at, str):
            logger.warning("Skipping post without valid created_at: %r", raw_post)
            return None

        try:
            post_created_at = datetime.fromisoformat(
                created_at.replace("Z", "+00:00")
            )
        except ValueError:
            logger.warning("Skipping post with malformed created_at: %r", raw_post)
            return None

        return self._as_utc(post_created_at) >= self._as_utc(created_after)

    def _as_utc(self, value: datetime) -> datetime:
        if value.tzinfo is None:
            return value.replace(tzinfo=UTC)
        return value.astimezone(UTC)

    def _fetch_with_cli(
        self, max_posts: int, created_after: datetime | None = None
    ) -> list[dict[str, Any]]:
        if shutil.which("truthbrush") is None:
            raise TruthSocialClientError(
                "Truthbrush is not installed. Install it with `pip install truthbrush`."
            )

        command = ["truthbrush", "--no-auth", "statuses"]
        if created_after is not None:
            command.extend(["--created-after", created_after.isoformat()])
        command.append(self.username)

        try:
            completed = subprocess.run(
                command,
                check=True,
                capture_output=True,
                text=True,
                timeout=120,
            )
        except FileNotFoundError as exc:
            raise TruthSocialClientError(
                "Truthbrush executable was not found on PATH."
            ) from exc
        except subprocess.TimeoutExpired as exc:
            raise TruthSocialClientError("Truthbrush command timed out.") from exc
        except subprocess.CalledProcessError as exc:
            stderr = exc.stderr.strip() if exc.stderr else "no stderr"
            raise TruthSocialClientError(
                f"Truthbrush command failed with exit code {exc.returncode}: {stderr}"
            ) from exc

        return list(self._parse_cli_output(completed.stdout))[:max_posts]

    def _parse_cli_output(self, stdout: str) -> Iterable[dict[str, Any]]:
        for line in stdout.splitlines():
            if not line.strip():
                continue

            try:
                parsed = json.loads(line)
            except json.JSONDecodeError:
                logger.warning("Skipping non-JSON Truthbrush output line: %s", line)
                continue

            if isinstance(parsed, list):
                for item in parsed:
                    if isinstance(item, dict):
                        yield item
                    else:
                        logger.warning("Skipping non-object Truthbrush list item: %r", item)
            elif isinstance(parsed, dict):
                yield parsed
            else:
                logger.warning("Skipping unsupported Truthbrush JSON payload: %r", parsed)
