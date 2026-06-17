"""Truth Social access via Playwright browser automation with stealth."""

from __future__ import annotations

import asyncio
import logging
from datetime import UTC, datetime
from typing import Any
from urllib.parse import urlparse

logger = logging.getLogger(__name__)

TRUTH_SOCIAL_BASE_URL = "https://truthsocial.com"
_DEFAULT_TIMEOUT_MS = 30_000
_EXTRA_WAIT_MS = 3_000
_DESKTOP_CHROME_USER_AGENT = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
    "AppleWebKit/537.36 (KHTML, like Gecko) "
    "Chrome/125.0.0.0 Safari/537.36"
)
_NON_CRITICAL_BLOCKED_PATH_PATTERNS = (
    "/api/v1/truth/ads/impression",
    "/api/v1/ads/",
    "/api/v3/pepe/instance",
    "ads/impression",
)
_STEALTH_INIT_SCRIPT = """
(() => {
  const defineGetter = (target, property, getter) => {
    try {
      Object.defineProperty(target, property, { get: getter, configurable: true });
    } catch (_) {
      // Leave the native browser value untouched if the property cannot be redefined.
    }
  };

  defineGetter(navigator, 'webdriver', () => undefined);
  defineGetter(navigator, 'languages', () => ['en-US', 'en']);
  defineGetter(navigator, 'plugins', () => [
    { name: 'Chrome PDF Plugin', filename: 'internal-pdf-viewer' },
    { name: 'Chrome PDF Viewer', filename: 'mhjfbmdgcfjbbpaeojofohoefgiehjai' },
    { name: 'Native Client', filename: 'internal-nacl-plugin' }
  ]);

  window.chrome = window.chrome || {};
  window.chrome.runtime = window.chrome.runtime || {};
  window.chrome.app = window.chrome.app || { isInstalled: false };
})();
"""

# Module-level references so tests can monkeypatch them.
try:
    from playwright.async_api import async_playwright
except ImportError:
    async_playwright = None  # type: ignore[assignment]

try:
    from playwright_stealth import Stealth
except ImportError:
    Stealth = None  # type: ignore[assignment]


class PlaywrightClientError(RuntimeError):
    """Raised when the Playwright client cannot fetch posts."""


class TruthSocialBlockedError(PlaywrightClientError):
    """Raised when Truth Social appears to block browser collection."""


class TruthSocialRateLimitedError(PlaywrightClientError):
    """Raised when Truth Social returns a rate-limit response."""


class TruthSocialAccessDeniedError(PlaywrightClientError):
    """Raised when Truth Social denies access or shows an auth gate."""


class TruthSocialTimeoutError(PlaywrightClientError):
    """Raised when navigation or relevant requests repeatedly time out."""


class TruthSocialEmptyResultError(PlaywrightClientError):
    """Raised when a profile load produces no usable posts unexpectedly."""


def is_non_critical_blocked_url(url: str) -> bool:
    """Return True for blocked Truth Social URLs that are not needed for posts."""
    try:
        path = urlparse(url).path
    except ValueError:
        path = url

    normalized = path.lower()
    return any(pattern in normalized for pattern in _NON_CRITICAL_BLOCKED_PATH_PATTERNS)


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
        if Stealth is None:
            raise PlaywrightClientError(
                "playwright-stealth is not installed. "
                "Install it with: pip install playwright-stealth>=2.0.0"
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
                    user_agent=_DESKTOP_CHROME_USER_AGENT,
                    viewport={"width": 1365, "height": 768},
                    locale="en-US",
                    timezone_id="Europe/Stockholm",
                    color_scheme="light",
                    device_scale_factor=1,
                    is_mobile=False,
                    has_touch=False,
                )
                await context.add_init_script(script=_STEALTH_INIT_SCRIPT)
                page = await context.new_page()
                stealth = Stealth()
                apply_stealth_async = getattr(stealth, "apply_stealth_async", None)
                if not callable(apply_stealth_async):
                    raise PlaywrightClientError(
                        "playwright-stealth is installed but does not expose "
                        "Stealth.apply_stealth_async. Install playwright-stealth>=2.0.0."
                    )

                await apply_stealth_async(page)

                captured: list[dict[str, Any]] = []
                deferred_blocked_paths: list[str] = []
                relevant_response_seen = False
                response_failure: PlaywrightClientError | None = None
                navigation_timeout: Exception | None = None

                async def on_response(response: Any) -> None:
                    nonlocal relevant_response_seen, response_failure
                    url: str = response.url
                    if not self._is_truth_social_url(url):
                        return
                    status = response.status
                    path = self._url_path(url)
                    if status == 429:
                        response_failure = TruthSocialRateLimitedError(
                            f"Truth Social returned HTTP 429 (rate limited). Path: {path}."
                        )
                        logger.warning(
                            "Truth Social rate limit detected. Url=%s Status=%s",
                            url,
                            status,
                        )
                        return
                    if status == 403 and is_non_critical_blocked_url(url):
                        deferred_blocked_paths.append(path)
                        logger.warning(
                            "Truth Social HTTP 403 treated as non-critical. Path=%s Status=%s Url=%s",
                            path,
                            status,
                            url,
                        )
                        return
                    if status == 403 and self._is_block_signal_path(path):
                        response_failure = TruthSocialBlockedError(
                            f"Truth Social returned HTTP 403 (Forbidden). Path: {path}."
                        )
                        logger.warning(
                            "Truth Social HTTP 403 treated as fatal block signal. Path=%s Status=%s Url=%s",
                            path,
                            status,
                            url,
                        )
                        return
                    if status in {401, 407} and self._is_block_signal_path(path):
                        response_failure = TruthSocialAccessDeniedError(
                            f"Truth Social denied access with HTTP {status}. Path: {path}."
                        )
                        logger.warning(
                            "Truth Social access denied. Url=%s Status=%s",
                            url,
                            status,
                        )
                        return

                    if "/api/v1/accounts/" not in url or "/statuses" not in url:
                        return
                    relevant_response_seen = True
                    if response.status != 200:
                        logger.warning(
                            "Statuses API returned HTTP %s for @%s. Path=%s",
                            status,
                            self.username,
                            path,
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
                except Exception as exc:
                    navigation_timeout = exc
                    logger.warning(
                        "Navigation to %s did not reach networkidle; "
                        "waiting %dms for pending responses.",
                        profile_url,
                        _EXTRA_WAIT_MS,
                    )
                    await page.wait_for_timeout(_EXTRA_WAIT_MS)

                if response_failure is not None:
                    raise response_failure

                logger.info(
                    "Playwright captured %s raw post(s) for @%s.",
                    len(captured),
                    self.username,
                )

                if not captured:
                    if deferred_blocked_paths:
                        logger.warning(
                            "No statuses response captured after non-critical Truth Social HTTP 403 response(s). Paths=%s",
                            ",".join(deferred_blocked_paths),
                        )
                    page_block_error = await self._detect_blocked_page(page)
                    if page_block_error is not None:
                        raise page_block_error
                    if navigation_timeout is not None:
                        raise TruthSocialTimeoutError(
                            "Truth Social page navigation timed out and no statuses "
                            f"were captured for @{self.username}: {navigation_timeout}"
                        )
                    if relevant_response_seen:
                        raise TruthSocialEmptyResultError(
                            "Truth Social statuses response contained no usable posts "
                            f"for @{self.username}."
                        )
                    logger.warning(
                        "No statuses API response captured for @%s. "
                        "Truth Social may have blocked the request or changed its page structure.",
                        self.username,
                    )
                    raise TruthSocialEmptyResultError(
                        "No public posts were found for "
                        f"@{self.username}. Truth Social may be blocking Playwright access "
                        "or the network payload changed."
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

    @staticmethod
    def _is_truth_social_url(url: str) -> bool:
        try:
            host = urlparse(url).netloc.lower()
        except ValueError:
            return False
        return host == "truthsocial.com" or host.endswith(".truthsocial.com")

    @staticmethod
    def _url_path(url: str) -> str:
        try:
            parsed = urlparse(url)
        except ValueError:
            return url
        path = parsed.path or "/"
        return f"{path}?{parsed.query}" if parsed.query else path

    def _is_block_signal_path(self, path: str) -> bool:
        normalized = path.lower()
        profile_path = f"/@{self.username.lower()}"
        return (
            normalized.startswith("/api/")
            or normalized.startswith(profile_path)
            or normalized.startswith("/login")
            or normalized == "/"
        )

    async def _detect_blocked_page(self, page: Any) -> PlaywrightClientError | None:
        page_url = str(getattr(page, "url", "") or "")
        title = await self._safe_page_text(page, "title")
        html = await self._safe_page_text(page, "content")
        haystack = f"{page_url}\n{title}\n{html[:10_000]}".lower()

        access_denied_indicators = [
            "log in",
            "login",
            "sign in",
            "access denied",
            "unauthorized",
            "authentication required",
        ]
        block_indicators = [
            "captcha",
            "cloudflare",
            "blocked",
            "temporarily unavailable",
            "verify you are human",
            "unusual traffic",
            "too many requests",
        ]

        if any(indicator in haystack for indicator in block_indicators):
            logger.warning(
                "Truth Social block page detected. Url=%s Title=%s",
                page_url,
                title[:120],
            )
            return TruthSocialBlockedError(
                "Truth Social appears to be blocking Playwright access."
            )

        if any(indicator in haystack for indicator in access_denied_indicators):
            logger.warning(
                "Truth Social access gate detected. Url=%s Title=%s",
                page_url,
                title[:120],
            )
            return TruthSocialAccessDeniedError(
                "Truth Social showed a login or access-denied page."
            )

        return None

    @staticmethod
    async def _safe_page_text(page: Any, method_name: str) -> str:
        method = getattr(page, method_name, None)
        if not callable(method):
            return ""
        try:
            value = method()
            if hasattr(value, "__await__"):
                value = await value
        except Exception as exc:
            logger.debug("Could not read page %s: %s", method_name, exc)
            return ""
        return value if isinstance(value, str) else ""
