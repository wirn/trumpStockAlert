from __future__ import annotations

import pytest

from collector.config import CollectorConfig


def test_collector_headless_defaults_to_true(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.delenv("COLLECTOR_HEADLESS", raising=False)

    config = CollectorConfig.from_env()

    assert config.collector_headless is True


def test_collector_headless_false_is_parsed(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("COLLECTOR_HEADLESS", "false")

    config = CollectorConfig.from_env()

    assert config.collector_headless is False


def test_collector_headless_invalid_value_raises(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("COLLECTOR_HEADLESS", "sometimes")

    with pytest.raises(ValueError, match="COLLECTOR_HEADLESS"):
        CollectorConfig.from_env()
