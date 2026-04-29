"""Backend API storage for normalized Truth Social posts."""

from __future__ import annotations

import json
import logging
import socket
import ssl
from dataclasses import dataclass
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

from collector.models import NormalizedPost
from collector.post_store_result import SavePostsResult

logger = logging.getLogger(__name__)


@dataclass(frozen=True)
class BackendPostResponse:
    status_code: int
    body: str
    payload: dict[str, Any]


class ApiTruthPostStore:
    def __init__(self, base_url: str, timeout_seconds: int = 30) -> None:
        self.base_url = base_url.rstrip("/")
        self.timeout_seconds = timeout_seconds

    @property
    def endpoint_url(self) -> str:
        return f"{self.base_url}/api/truth-posts"

    def save_posts(self, posts: list[NormalizedPost]) -> SavePostsResult:
        saved_posts: list[NormalizedPost] = []
        already_existing_count = 0
        failed_count = 0

        for post in posts:
            try:
                response = self._post(post)
            except Exception as exc:
                failed_count += 1
                logger.error(
                    "FailedPost ExternalId=%s StatusCode=unavailable ResponseBody=%s",
                    post.externalId,
                    exc,
                )
                continue

            if response.status_code == 201:
                saved_posts.append(post)
                logger.info(
                    "Saved post %s/%s to backend database as row %s.",
                    post.source,
                    post.externalId,
                    response.payload.get("id"),
                )
            elif self._is_existing_post_response(response):
                already_existing_count += 1
                logger.info(
                    "Post %s/%s already exists in backend database. StatusCode: %s. Row: %s.",
                    post.source,
                    post.externalId,
                    response.status_code,
                    response.payload.get("id"),
                )
            else:
                failed_count += 1
                logger.error(
                    "FailedPost ExternalId=%s StatusCode=%s ResponseBody=%s",
                    post.externalId,
                    response.status_code,
                    self._compact_response_body(response.body),
                )

        logger.info(
            "Backend save summary. Saved: %s. Skipped: %s. Failed: %s.",
            len(saved_posts),
            already_existing_count,
            failed_count,
        )

        return SavePostsResult(saved_posts, already_existing_count, failed_count)

    def _post(self, post: NormalizedPost) -> BackendPostResponse:
        body = json.dumps(post.to_dict()).encode("utf-8")
        request = Request(
            url=self.endpoint_url,
            data=body,
            headers={"Content-Type": "application/json"},
            method="POST",
        )

        try:
            with self._open(request, timeout=self.timeout_seconds) as response:
                response_body = response.read().decode("utf-8", errors="replace")
                return BackendPostResponse(
                    status_code=response.status,
                    body=response_body,
                    payload=self._read_json(response_body),
                )
        except HTTPError as exc:
            error_body = exc.read().decode("utf-8", errors="replace")
            logger.error(
                "FailedPost ExternalId=%s StatusCode=%s ResponseBody=%s",
                post.externalId,
                exc.code,
                self._compact_response_body(error_body),
            )
            return BackendPostResponse(
                status_code=exc.code,
                body=error_body,
                payload=self._read_json(error_body),
            )
        except TimeoutError as exc:
            raise RuntimeError(self._connection_error_message("timed out")) from exc
        except ssl.SSLError as exc:
            raise RuntimeError(self._connection_error_message(str(exc))) from exc
        except socket.timeout as exc:
            raise RuntimeError(self._connection_error_message("timed out")) from exc
        except URLError as exc:
            raise RuntimeError(
                self._connection_error_message(str(exc.reason))
            ) from exc

    def _is_existing_post_response(self, response: BackendPostResponse) -> bool:
        if response.status_code == 200:
            return True

        if response.status_code != 409:
            return False

        body = response.body.lower()
        return "duplicate" in body or "already" in body or "exists" in body

    def _read_json(self, payload: str) -> dict[str, Any]:
        if not payload:
            return {}

        try:
            parsed = json.loads(payload)
        except json.JSONDecodeError:
            return {}

        if not isinstance(parsed, dict):
            return {}
        return parsed

    def _compact_response_body(self, body: str, max_length: int = 2000) -> str:
        compacted = " ".join(body.split())
        if len(compacted) <= max_length:
            return compacted
        return f"{compacted[:max_length]}..."

    def _connection_error_message(self, reason: str) -> str:
        hint = (
            "Check that the backend API is running and that "
            "TRUTH_POST_API_BASE_URL is set to http://localhost:5044 for local runs."
        )
        if self.base_url.lower().startswith("https://localhost:5044"):
            hint = (
                "The local backend on port 5044 is HTTP-only. Use "
                "TRUTH_POST_API_BASE_URL=http://localhost:5044, not HTTPS."
            )

        return (
            f"Could not reach backend API endpoint {self.endpoint_url}: {reason}. "
            f"{hint}"
        )

    def _open(self, request: Request, timeout: int) -> Any:
        return urlopen(request, timeout=timeout)
