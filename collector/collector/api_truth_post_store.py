"""Backend API storage for normalized Truth Social posts."""

from __future__ import annotations

import json
import logging
import socket
import ssl
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

from collector.models import NormalizedPost
from collector.post_store_result import SavePostsResult

logger = logging.getLogger(__name__)


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

        for post in posts:
            status_code, response = self._post(post)
            if status_code == 201:
                saved_posts.append(post)
                logger.info(
                    "Saved post %s/%s to backend database as row %s.",
                    post.source,
                    post.externalId,
                    response.get("id"),
                )
            elif status_code == 200:
                already_existing_count += 1
                logger.info(
                    "Post %s/%s already exists in backend database as row %s.",
                    post.source,
                    post.externalId,
                    response.get("id"),
                )
            else:
                raise RuntimeError(
                    f"Unexpected backend response {status_code} for post "
                    f"{post.source}/{post.externalId}."
                )

        return SavePostsResult(saved_posts, already_existing_count)

    def _post(self, post: NormalizedPost) -> tuple[int, dict[str, Any]]:
        body = json.dumps(post.to_dict()).encode("utf-8")
        request = Request(
            url=self.endpoint_url,
            data=body,
            headers={"Content-Type": "application/json"},
            method="POST",
        )

        try:
            with urlopen(request, timeout=self.timeout_seconds) as response:
                payload = self._read_json(response.read())
                return response.status, payload
        except HTTPError as exc:
            error_body = exc.read().decode("utf-8", errors="replace")
            raise RuntimeError(
                f"Backend rejected post {post.source}/{post.externalId} "
                f"with HTTP {exc.code}: {error_body}"
            ) from exc
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

    def _read_json(self, payload: bytes) -> dict[str, Any]:
        if not payload:
            return {}

        parsed = json.loads(payload.decode("utf-8"))
        if not isinstance(parsed, dict):
            raise RuntimeError("Backend returned a non-object JSON response.")
        return parsed

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
