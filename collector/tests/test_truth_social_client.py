from __future__ import annotations

import sys
import types
from datetime import UTC, datetime

from collector.truth_social_client import TruthSocialClient


def test_fetch_with_python_api_supports_truthbrush_without_require_auth(monkeypatch):
    calls: list[dict[str, object]] = []

    class Api:
        def __init__(self) -> None:
            calls.append({"constructor": "without_require_auth"})

        def pull_statuses(self, username, created_after=None):
            calls.append({"username": username, "created_after": created_after})
            yield {
                "id": "2",
                "created_at": "2026-04-29T09:00:00+00:00",
                "content": "<p>new</p>",
            }
            yield {
                "id": "1",
                "created_at": "2026-04-29T07:00:00+00:00",
                "content": "<p>old</p>",
            }

    install_truthbrush_api(monkeypatch, Api)

    client = TruthSocialClient("@realDonaldTrump")
    posts = client.fetch_latest_posts(
        max_posts=10,
        created_after=datetime(2026, 4, 29, 8, tzinfo=UTC),
    )

    assert [post["id"] for post in posts] == ["2"]
    assert calls[0] == {"constructor": "without_require_auth"}
    assert calls[1]["username"] == "realDonaldTrump"


def test_fetch_with_python_api_uses_require_auth_false_when_supported(monkeypatch):
    constructor_kwargs: list[dict[str, object]] = []

    class Api:
        def __init__(self, *, require_auth=True) -> None:
            constructor_kwargs.append({"require_auth": require_auth})

        def pull_statuses(self, username, created_after=None):
            yield {
                "id": "1",
                "created_at": "2026-04-29T09:00:00+00:00",
                "content": "<p>hello</p>",
            }

    install_truthbrush_api(monkeypatch, Api)

    posts = TruthSocialClient("realDonaldTrump").fetch_latest_posts(max_posts=1)

    assert [post["id"] for post in posts] == ["1"]
    assert constructor_kwargs == [{"require_auth": False}]


def install_truthbrush_api(monkeypatch, api_type):
    truthbrush_module = types.ModuleType("truthbrush")
    api_module = types.ModuleType("truthbrush.api")
    api_module.Api = api_type

    monkeypatch.setitem(sys.modules, "truthbrush", truthbrush_module)
    monkeypatch.setitem(sys.modules, "truthbrush.api", api_module)
