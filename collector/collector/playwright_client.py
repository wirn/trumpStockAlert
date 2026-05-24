"""Truth Social access via Playwright browser automation with stealth."""

from __future__ import annotations

import asyncio
import logging
from datetime import UTC, datetime
from typing import Any

logger = logging.getLogger(__name__)

TRUTH_SOCIAL_BASE_URL = "https://truthsocial.com"
_DEFAULT_TIMEOUT_MS = 30_000
_EXTRA_WAIT_MS = 3_000

# Module-level references so tests can monkeypatch them.
try:
    from playwright.async_api import async_playwright
    from playwright_stealth import stealth_async  # type: ignore[import-not-found]
except ImportError:
    async_playwright = None  # type: ignore[assignment]
    stealth_async = None  # type: ignore[assignment]


class PlaywrightClientError(RuntimeError):
    """Raised when the Playwright client cannot fetch posts."""


class PlaywrightTruthSocialClient:
    """Fetches recent Truth Social posts using a headless Chromium browser with stealth.

    Intercepts the Mastodon-compatible /api/v1/accounts/{id}/statuses response
    that Truth Social loads when rendering the profile page.
    """

    def __init__(
        self,
        username: str,
        headless: bool = True,
        timeout_seconds: int = 30,
    ) -> None:
        self.username = username.lstrip("@")
        self.headless = headless
        self._timeout_ms = timeout_seconds * 1000

    def fetch_latest_posts(
        self, max_posts: int, created_after: datetime | None = None
    ) -> list[dict[str, Any]]:
        try:
            return asyncio.run(self._fetch_async(max_posts, created_after))
        except PlaywrightClientError:
            raise
        except Exception as exc:
            raise PlaywrightClientError(
                f"Playwright fetch failed for @{self.username}: {exc}"
            ) from exc

    async def _fetch_async(
        self,
        max_posts: int,
        created_after: datetime | None,
    ) -> list[dict[str, Any]]:
        if async_playwright is None:
            raise PlaywrightClientError(
                "playwright is not installed. "
                "Install it with: pip install playwright playwright-stealth && playwright install chromium"
            )
        if stealth_async is None:
            raise PlaywrightClientError(
                "playwright-stealth is not installed. "
                "Install it with: pip install playwright-stealth"
            )

        async with async_playwright() as pw:
            browser = await pw.chromium.launch(
                headless=self.headless,
                # Required in Docker: no sandbox (kernel namespaces typically
                # unavailable) and route /dev/shm writes through /tmp to avoid
                # the 64 MB Docker default limit.
                args=["--no-sandbox", "--disable-dev-shm-usage"],
            )
            try:
                context = await browser.new_context(
                    viewport={"width": 1280, "height": 900},
                    locale="en-US",
                )
                page = await context.new_page()
                await stealth_async(page)

                captured: list[dict[str, Any]] = []

                async def on_response(response: Any) -> None:
                    url: str = response.url
                    if "/api/v1/accounts/" not in url or "/statuses" not in url:
                        return
                    if response.status != 200:
                        logger.warning(
                            "Statuses API returned HTTP %s for @%s.",
                            response.status,
                            self.username,
                        )
                        return
                    try:
                        data = await response.json()
                        if isinstance(data, list):
                            captured.extend(data)
                            logger.debug("Captured %s post(s) from %s.", len(data), url)
                    except Exception as exc:
                        logger.warning("Failed to parse statuses response: %s", exc)

                page.on("response", on_response)

                profile_url = f"{TRUTH_SOCIAL_BASE_URL}/@{self.username}"
                logger.info("Playwright navigating to %s.", profile_url)

                try:
                    await page.goto(
                        profile_url,
                        wait_until="networkidle",
                        timeout=self._timeout_ms,
                    )
                except Exception:
                    logger.warning(
                        "Navigation to %s did not reach networkidle; "
                        "waiting %dms for pending responses.",
                        profile_url,
                        _EXTRA_WAIT_MS,
                    )
                    await page.wait_for_timeout(_EXTRA_WAIT_MS)

                logger.info(
                    "Playwright captured %s raw post(s) for @%s.",
                    len(captured),
                    self.username,
                )

                if not captured:
                    logger.warning(
                        "No statuses API response captured for @%s. "
                        "Truth Social may have blocked the request or changed its page structure.",
                        self.username,
                    )

                filtered = self._filter_by_created_after(captured, created_after)
                return filtered[:max_posts]

            finally:
                await browser.close()

    def _filter_by_created_after(
        self,
        posts: list[dict[str, Any]],
        created_after: datetime | None,
    ) -> list[dict[str, Any]]:
        if created_after is None:
            return posts

        cutoff = self._to_utc(created_after)
        result: list[dict[str, Any]] = []
        for post in posts:
            ts = post.get("created_at")
            if not isinstance(ts, str):
                logger.warning(
                    "Skipping post %r: missing created_at.", post.get("id")
                )
                continue
            try:
                post_dt = datetime.fromisoformat(ts.replace("Z", "+00:00"))
            except ValueError:
                logger.warning("Skipping post with malformed created_at: %r.", ts)
                continue
            if self._to_utc(post_dt) >= cutoff:
                result.append(post)
        return result

    @staticmethod
    def _to_utc(value: datetime) -> datetime:
        if value.tzinfo is None:
            return value.replace(tzinfo=UTC)
        return value.astimezone(UTC)
