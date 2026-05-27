from __future__ import annotations

import sys
from collections.abc import Callable
from datetime import UTC, datetime
from unittest.mock import AsyncMock, MagicMock

import pytest

from collector.playwright_client import (
    PlaywrightClientError,
    PlaywrightTruthSocialClient,
    TruthSocialAccessDeniedError,
    TruthSocialBlockedError,
    TruthSocialEmptyResultError,
    TruthSocialRateLimitedError,
    TruthSocialTimeoutError,
    is_non_critical_blocked_url,
)

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

SAMPLE_POSTS = [
    {"id": "3", "created_at": "2026-05-18T12:00:00Z", "content": "<p>newest</p>"},
    {"id": "2", "created_at": "2026-05-18T10:00:00Z", "content": "<p>middle</p>"},
    {"id": "1", "created_at": "2026-05-18T08:00:00Z", "content": "<p>oldest</p>"},
]

STATUSES_URL = "https://truthsocial.com/api/v1/accounts/12345/statuses"
UNRELATED_URL = "https://truthsocial.com/assets/app.js"


def make_mock_response(posts: list, url: str = STATUSES_URL, status: int = 200) -> MagicMock:
    mock = MagicMock()
    mock.url = url
    mock.status = status
    mock.json = AsyncMock(return_value=posts)
    return mock


def make_playwright_stack(
    posts_to_return: list,
    response_url: str = STATUSES_URL,
    response_status: int = 200,
    goto_raises: Exception | None = None,
    page_url: str = "https://truthsocial.com/@realDonaldTrump",
    page_title: str = "realDonaldTrump (@realDonaldTrump) - Truth Social",
    page_content: str = "<html><body>public profile</body></html>",
) -> tuple[MagicMock, MagicMock, AsyncMock]:
    """Return Playwright/Stealth mocks that fire a response during goto."""
    registered: list[Callable] = []

    async def mock_goto(*args, **kwargs) -> None:
        if goto_raises is not None:
            raise goto_raises
        mock_resp = make_mock_response(posts_to_return, response_url, response_status)
        for handler in registered:
            await handler(mock_resp)

    mock_page = MagicMock()
    mock_page.on = lambda event, fn: registered.append(fn) if event == "response" else None
    mock_page.goto = mock_goto
    mock_page.wait_for_timeout = AsyncMock()
    mock_page.url = page_url
    mock_page.title = AsyncMock(return_value=page_title)
    mock_page.content = AsyncMock(return_value=page_content)

    mock_context = MagicMock()
    mock_context.add_init_script = AsyncMock()
    mock_context.new_page = AsyncMock(return_value=mock_page)

    mock_browser = MagicMock()
    mock_browser.new_context = AsyncMock(return_value=mock_context)
    mock_browser.close = AsyncMock()

    mock_chromium = MagicMock()
    mock_chromium.launch = AsyncMock(return_value=mock_browser)

    mock_pw = MagicMock()
    mock_pw.chromium = mock_chromium
    mock_pw.__aenter__ = AsyncMock(return_value=mock_pw)
    mock_pw.__aexit__ = AsyncMock(return_value=False)

    mock_async_playwright = MagicMock(return_value=mock_pw)
    mock_async_playwright.mock_context = mock_context
    mock_async_playwright.mock_browser = mock_browser
    mock_stealth_instance = MagicMock()
    mock_stealth_instance.apply_stealth_async = AsyncMock()
    mock_stealth_type = MagicMock(return_value=mock_stealth_instance)

    return mock_async_playwright, mock_stealth_type, mock_stealth_instance.apply_stealth_async


def make_playwright_stack_with_responses(
    responses: list[MagicMock],
) -> tuple[MagicMock, MagicMock, AsyncMock]:
    """Return Playwright/Stealth mocks that fire multiple responses during goto."""
    registered: list[Callable] = []

    async def mock_goto(*args, **kwargs) -> None:
        for handler in registered:
            for response in responses:
                await handler(response)

    mock_page = MagicMock()
    mock_page.on = lambda event, fn: registered.append(fn) if event == "response" else None
    mock_page.goto = mock_goto
    mock_page.wait_for_timeout = AsyncMock()
    mock_page.url = "https://truthsocial.com/@realDonaldTrump"
    mock_page.title = AsyncMock(return_value="realDonaldTrump (@realDonaldTrump) - Truth Social")
    mock_page.content = AsyncMock(return_value="<html><body>public profile</body></html>")

    mock_context = MagicMock()
    mock_context.add_init_script = AsyncMock()
    mock_context.new_page = AsyncMock(return_value=mock_page)

    mock_browser = MagicMock()
    mock_browser.new_context = AsyncMock(return_value=mock_context)
    mock_browser.close = AsyncMock()

    mock_chromium = MagicMock()
    mock_chromium.launch = AsyncMock(return_value=mock_browser)

    mock_pw = MagicMock()
    mock_pw.chromium = mock_chromium
    mock_pw.__aenter__ = AsyncMock(return_value=mock_pw)
    mock_pw.__aexit__ = AsyncMock(return_value=False)

    mock_async_playwright = MagicMock(return_value=mock_pw)
    mock_async_playwright.mock_context = mock_context
    mock_async_playwright.mock_browser = mock_browser
    mock_stealth_instance = MagicMock()
    mock_stealth_instance.apply_stealth_async = AsyncMock()
    mock_stealth_type = MagicMock(return_value=mock_stealth_instance)

    return mock_async_playwright, mock_stealth_type, mock_stealth_instance.apply_stealth_async


# ---------------------------------------------------------------------------
# _filter_by_created_after (pure logic)
# ---------------------------------------------------------------------------

def test_filter_returns_all_when_no_cutoff():
    client = PlaywrightTruthSocialClient("realDonaldTrump")
    result = client._filter_by_created_after(SAMPLE_POSTS, None)
    assert result == SAMPLE_POSTS


def test_filter_keeps_posts_on_or_after_cutoff():
    client = PlaywrightTruthSocialClient("realDonaldTrump")
    cutoff = datetime(2026, 5, 18, 10, tzinfo=UTC)
    result = client._filter_by_created_after(SAMPLE_POSTS, cutoff)
    assert [p["id"] for p in result] == ["3", "2"]


def test_filter_excludes_all_posts_before_cutoff():
    client = PlaywrightTruthSocialClient("realDonaldTrump")
    cutoff = datetime(2026, 5, 19, tzinfo=UTC)
    result = client._filter_by_created_after(SAMPLE_POSTS, cutoff)
    assert result == []


def test_filter_skips_posts_with_missing_created_at():
    client = PlaywrightTruthSocialClient("realDonaldTrump")
    posts = [{"id": "x"}, {"id": "1", "created_at": "2026-05-18T12:00:00Z"}]
    cutoff = datetime(2026, 5, 18, tzinfo=UTC)
    result = client._filter_by_created_after(posts, cutoff)
    assert [p["id"] for p in result] == ["1"]


def test_filter_skips_posts_with_malformed_created_at():
    client = PlaywrightTruthSocialClient("realDonaldTrump")
    posts = [
        {"id": "bad", "created_at": "not-a-date"},
        {"id": "ok", "created_at": "2026-05-18T12:00:00Z"},
    ]
    cutoff = datetime(2026, 5, 18, tzinfo=UTC)
    result = client._filter_by_created_after(posts, cutoff)
    assert [p["id"] for p in result] == ["ok"]


# ---------------------------------------------------------------------------
# _to_utc
# ---------------------------------------------------------------------------

def test_to_utc_adds_utc_to_naive_datetime():
    naive = datetime(2026, 5, 18, 10, 0, 0)
    result = PlaywrightTruthSocialClient._to_utc(naive)
    assert result.tzinfo == UTC


def test_to_utc_converts_aware_datetime_to_utc():
    from datetime import timezone, timedelta
    cet = timezone(timedelta(hours=2))
    aware = datetime(2026, 5, 18, 12, 0, 0, tzinfo=cet)
    result = PlaywrightTruthSocialClient._to_utc(aware)
    assert result == datetime(2026, 5, 18, 10, 0, 0, tzinfo=UTC)


# ---------------------------------------------------------------------------
# Import error handling
# ---------------------------------------------------------------------------

def test_missing_playwright_raises_client_error(monkeypatch):
    monkeypatch.setattr("collector.playwright_client.async_playwright", None)
    client = PlaywrightTruthSocialClient("realDonaldTrump")
    with pytest.raises(PlaywrightClientError, match="playwright is not installed"):
        client.fetch_latest_posts(max_posts=5)


def test_missing_stealth_raises_client_error(monkeypatch):
    monkeypatch.setattr("collector.playwright_client.Stealth", None)
    # async_playwright must be non-None; use any truthy value
    monkeypatch.setattr("collector.playwright_client.async_playwright", MagicMock())
    client = PlaywrightTruthSocialClient("realDonaldTrump")
    with pytest.raises(PlaywrightClientError, match="playwright-stealth is not installed"):
        client.fetch_latest_posts(max_posts=5)


def test_unsupported_stealth_api_raises_client_error(monkeypatch):
    mock_pw, mock_stealth_type, _ = make_playwright_stack(SAMPLE_POSTS)
    mock_stealth_instance = MagicMock()
    mock_stealth_instance.apply_stealth_async = None
    mock_stealth_type.return_value = mock_stealth_instance
    monkeypatch.setattr("collector.playwright_client.async_playwright", mock_pw)
    monkeypatch.setattr("collector.playwright_client.Stealth", mock_stealth_type)

    client = PlaywrightTruthSocialClient("realDonaldTrump")
    with pytest.raises(PlaywrightClientError, match="Stealth.apply_stealth_async"):
        client.fetch_latest_posts(max_posts=5)


# ---------------------------------------------------------------------------
# Full fetch (browser mocked)
# ---------------------------------------------------------------------------

def test_fetch_returns_posts_from_intercepted_response(monkeypatch):
    mock_pw, mock_stealth_type, mock_apply_stealth = make_playwright_stack(SAMPLE_POSTS)
    monkeypatch.setattr("collector.playwright_client.async_playwright", mock_pw)
    monkeypatch.setattr("collector.playwright_client.Stealth", mock_stealth_type)

    client = PlaywrightTruthSocialClient("realDonaldTrump")
    posts = client.fetch_latest_posts(max_posts=10)

    assert [p["id"] for p in posts] == ["3", "2", "1"]
    mock_apply_stealth.assert_awaited_once()


def test_fetch_launches_headless_by_default(monkeypatch):
    mock_pw, mock_stealth_type, _ = make_playwright_stack(SAMPLE_POSTS)
    monkeypatch.setattr("collector.playwright_client.async_playwright", mock_pw)
    monkeypatch.setattr("collector.playwright_client.Stealth", mock_stealth_type)

    client = PlaywrightTruthSocialClient("realDonaldTrump")
    client.fetch_latest_posts(max_posts=10)

    launch_kwargs = mock_pw.return_value.chromium.launch.await_args.kwargs
    assert launch_kwargs["headless"] is True


def test_fetch_uses_desktop_context_fingerprint(monkeypatch):
    mock_pw, mock_stealth_type, _ = make_playwright_stack(SAMPLE_POSTS)
    monkeypatch.setattr("collector.playwright_client.async_playwright", mock_pw)
    monkeypatch.setattr("collector.playwright_client.Stealth", mock_stealth_type)

    client = PlaywrightTruthSocialClient("realDonaldTrump", headless=False)
    client.fetch_latest_posts(max_posts=10)

    launch_kwargs = mock_pw.return_value.chromium.launch.await_args.kwargs
    context_kwargs = mock_pw.mock_context.new_page.await_args
    new_context_kwargs = mock_pw.mock_browser.new_context.await_args.kwargs

    assert launch_kwargs["headless"] is False
    assert "HeadlessChrome" not in new_context_kwargs["user_agent"]
    assert "Chrome/" in new_context_kwargs["user_agent"]
    assert new_context_kwargs["viewport"] == {"width": 1365, "height": 768}
    assert new_context_kwargs["locale"] == "en-US"
    assert new_context_kwargs["timezone_id"] == "Europe/Stockholm"
    assert new_context_kwargs["color_scheme"] == "light"
    assert new_context_kwargs["device_scale_factor"] == 1
    assert new_context_kwargs["is_mobile"] is False
    assert new_context_kwargs["has_touch"] is False
    assert context_kwargs is not None


def test_fetch_registers_automation_reduction_init_script(monkeypatch):
    mock_pw, mock_stealth_type, _ = make_playwright_stack(SAMPLE_POSTS)
    monkeypatch.setattr("collector.playwright_client.async_playwright", mock_pw)
    monkeypatch.setattr("collector.playwright_client.Stealth", mock_stealth_type)

    client = PlaywrightTruthSocialClient("realDonaldTrump")
    client.fetch_latest_posts(max_posts=10)

    script = mock_pw.mock_context.add_init_script.await_args.kwargs["script"]
    assert "navigator, 'webdriver'" in script
    assert "navigator, 'languages'" in script
    assert "navigator, 'plugins'" in script
    assert "window.chrome" in script


def test_fetch_respects_max_posts(monkeypatch):
    mock_pw, mock_stealth_type, _ = make_playwright_stack(SAMPLE_POSTS)
    monkeypatch.setattr("collector.playwright_client.async_playwright", mock_pw)
    monkeypatch.setattr("collector.playwright_client.Stealth", mock_stealth_type)

    client = PlaywrightTruthSocialClient("realDonaldTrump")
    posts = client.fetch_latest_posts(max_posts=2)

    assert len(posts) == 2
    assert posts[0]["id"] == "3"


def test_fetch_applies_created_after_filter(monkeypatch):
    mock_pw, mock_stealth_type, _ = make_playwright_stack(SAMPLE_POSTS)
    monkeypatch.setattr("collector.playwright_client.async_playwright", mock_pw)
    monkeypatch.setattr("collector.playwright_client.Stealth", mock_stealth_type)

    client = PlaywrightTruthSocialClient("realDonaldTrump")
    cutoff = datetime(2026, 5, 18, 11, tzinfo=UTC)
    posts = client.fetch_latest_posts(max_posts=10, created_after=cutoff)

    assert [p["id"] for p in posts] == ["3"]


def test_fetch_raises_when_no_statuses_response_is_captured(monkeypatch):
    mock_pw, mock_stealth_type, _ = make_playwright_stack(
        SAMPLE_POSTS, response_url=UNRELATED_URL
    )
    monkeypatch.setattr("collector.playwright_client.async_playwright", mock_pw)
    monkeypatch.setattr("collector.playwright_client.Stealth", mock_stealth_type)

    client = PlaywrightTruthSocialClient("realDonaldTrump")
    with pytest.raises(TruthSocialEmptyResultError, match="No public posts"):
        client.fetch_latest_posts(max_posts=10)


def test_fetch_raises_blocked_error_on_403_response(monkeypatch):
    mock_pw, mock_stealth_type, _ = make_playwright_stack(
        SAMPLE_POSTS, response_status=403
    )
    monkeypatch.setattr("collector.playwright_client.async_playwright", mock_pw)
    monkeypatch.setattr("collector.playwright_client.Stealth", mock_stealth_type)

    client = PlaywrightTruthSocialClient("realDonaldTrump")
    with pytest.raises(TruthSocialBlockedError, match="HTTP 403"):
        client.fetch_latest_posts(max_posts=10)


@pytest.mark.parametrize(
    "url",
    [
        "https://truthsocial.com/api/v1/truth/ads/impression?provider=revcontent&source=modal",
        "https://truthsocial.com/api/v1/ads/some-placement",
        "https://truthsocial.com/anything/ads/impression",
    ],
)
def test_non_critical_blocked_url_matches_ad_impression_paths(url):
    assert is_non_critical_blocked_url(url)


def test_fetch_ignores_non_critical_ad_impression_403(monkeypatch, caplog):
    responses = [
        make_mock_response(
            [],
            url="https://truthsocial.com/api/v1/truth/ads/impression?provider=revcontent&source=modal",
            status=403,
        ),
        make_mock_response(SAMPLE_POSTS, url=STATUSES_URL, status=200),
    ]
    mock_pw, mock_stealth_type, _ = make_playwright_stack_with_responses(responses)
    monkeypatch.setattr("collector.playwright_client.async_playwright", mock_pw)
    monkeypatch.setattr("collector.playwright_client.Stealth", mock_stealth_type)

    client = PlaywrightTruthSocialClient("realDonaldTrump")

    with caplog.at_level("WARNING"):
        posts = client.fetch_latest_posts(max_posts=10)

    assert [p["id"] for p in posts] == ["3", "2", "1"]
    assert "Ignoring non-critical Truth Social HTTP 403" in caplog.text


def test_fetch_raises_rate_limited_error_on_429_response(monkeypatch):
    mock_pw, mock_stealth_type, _ = make_playwright_stack(
        SAMPLE_POSTS, response_status=429
    )
    monkeypatch.setattr("collector.playwright_client.async_playwright", mock_pw)
    monkeypatch.setattr("collector.playwright_client.Stealth", mock_stealth_type)

    client = PlaywrightTruthSocialClient("realDonaldTrump")
    with pytest.raises(TruthSocialRateLimitedError, match="HTTP 429"):
        client.fetch_latest_posts(max_posts=10)


def test_fetch_raises_access_denied_error_on_auth_failure_response(monkeypatch):
    mock_pw, mock_stealth_type, _ = make_playwright_stack(
        SAMPLE_POSTS, response_status=401
    )
    monkeypatch.setattr("collector.playwright_client.async_playwright", mock_pw)
    monkeypatch.setattr("collector.playwright_client.Stealth", mock_stealth_type)

    client = PlaywrightTruthSocialClient("realDonaldTrump")
    with pytest.raises(TruthSocialAccessDeniedError, match="HTTP 401"):
        client.fetch_latest_posts(max_posts=10)


def test_fetch_raises_blocked_error_on_captcha_page(monkeypatch):
    mock_pw, mock_stealth_type, _ = make_playwright_stack(
        [],
        response_url=UNRELATED_URL,
        page_title="Just a moment...",
        page_content="<html><body>Verify you are human before continuing</body></html>",
    )
    monkeypatch.setattr("collector.playwright_client.async_playwright", mock_pw)
    monkeypatch.setattr("collector.playwright_client.Stealth", mock_stealth_type)

    client = PlaywrightTruthSocialClient("realDonaldTrump")
    with pytest.raises(TruthSocialBlockedError, match="blocking Playwright access"):
        client.fetch_latest_posts(max_posts=10)


def test_fetch_raises_access_denied_error_on_login_page(monkeypatch):
    mock_pw, mock_stealth_type, _ = make_playwright_stack(
        [],
        response_url=UNRELATED_URL,
        page_title="Log in - Truth Social",
        page_content="<html><body>Authentication required. Please sign in.</body></html>",
    )
    monkeypatch.setattr("collector.playwright_client.async_playwright", mock_pw)
    monkeypatch.setattr("collector.playwright_client.Stealth", mock_stealth_type)

    client = PlaywrightTruthSocialClient("realDonaldTrump")
    with pytest.raises(TruthSocialAccessDeniedError, match="login or access-denied"):
        client.fetch_latest_posts(max_posts=10)


def test_fetch_raises_timeout_when_navigation_times_out_without_posts(monkeypatch):
    mock_pw, mock_stealth_type, _ = make_playwright_stack(
        SAMPLE_POSTS, goto_raises=TimeoutError("networkidle timeout")
    )
    monkeypatch.setattr("collector.playwright_client.async_playwright", mock_pw)
    monkeypatch.setattr("collector.playwright_client.Stealth", mock_stealth_type)

    client = PlaywrightTruthSocialClient("realDonaldTrump")
    with pytest.raises(TruthSocialTimeoutError, match="navigation timed out"):
        client.fetch_latest_posts(max_posts=10)


def test_fetch_strips_at_prefix_from_username(monkeypatch):
    mock_pw, mock_stealth_type, _ = make_playwright_stack(SAMPLE_POSTS)
    monkeypatch.setattr("collector.playwright_client.async_playwright", mock_pw)
    monkeypatch.setattr("collector.playwright_client.Stealth", mock_stealth_type)

    client = PlaywrightTruthSocialClient("@realDonaldTrump")
    assert client.username == "realDonaldTrump"
