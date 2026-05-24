from __future__ import annotations

import sys
from collections.abc import Callable
from datetime import UTC, datetime
from unittest.mock import AsyncMock, MagicMock

import pytest

from collector.playwright_client import PlaywrightClientError, PlaywrightTruthSocialClient

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

    mock_context = MagicMock()
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


def test_fetch_ignores_unrelated_responses(monkeypatch):
    mock_pw, mock_stealth_type, _ = make_playwright_stack(
        SAMPLE_POSTS, response_url=UNRELATED_URL
    )
    monkeypatch.setattr("collector.playwright_client.async_playwright", mock_pw)
    monkeypatch.setattr("collector.playwright_client.Stealth", mock_stealth_type)

    client = PlaywrightTruthSocialClient("realDonaldTrump")
    posts = client.fetch_latest_posts(max_posts=10)

    assert posts == []


def test_fetch_ignores_non_200_statuses_responses(monkeypatch):
    mock_pw, mock_stealth_type, _ = make_playwright_stack(
        SAMPLE_POSTS, response_status=403
    )
    monkeypatch.setattr("collector.playwright_client.async_playwright", mock_pw)
    monkeypatch.setattr("collector.playwright_client.Stealth", mock_stealth_type)

    client = PlaywrightTruthSocialClient("realDonaldTrump")
    posts = client.fetch_latest_posts(max_posts=10)

    assert posts == []


def test_fetch_continues_after_networkidle_timeout(monkeypatch):
    """When goto raises (e.g. networkidle timeout), the client waits and returns whatever was captured."""
    mock_pw, mock_stealth_type, _ = make_playwright_stack(
        SAMPLE_POSTS, goto_raises=TimeoutError("networkidle timeout")
    )
    monkeypatch.setattr("collector.playwright_client.async_playwright", mock_pw)
    monkeypatch.setattr("collector.playwright_client.Stealth", mock_stealth_type)

    client = PlaywrightTruthSocialClient("realDonaldTrump")
    # No posts captured because the response handler never fires (goto raised before it)
    posts = client.fetch_latest_posts(max_posts=10)
    assert posts == []


def test_fetch_strips_at_prefix_from_username(monkeypatch):
    mock_pw, mock_stealth_type, _ = make_playwright_stack(SAMPLE_POSTS)
    monkeypatch.setattr("collector.playwright_client.async_playwright", mock_pw)
    monkeypatch.setattr("collector.playwright_client.Stealth", mock_stealth_type)

    client = PlaywrightTruthSocialClient("@realDonaldTrump")
    assert client.username == "realDonaldTrump"
