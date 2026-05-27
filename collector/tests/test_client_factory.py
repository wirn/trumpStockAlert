from __future__ import annotations

from unittest.mock import MagicMock

from collector.client_factory import create_client
from collector.config import CollectorConfig


def test_create_playwright_client_passes_headless_config(monkeypatch) -> None:
    mock_client_type = MagicMock()
    monkeypatch.setattr(
        "collector.playwright_client.PlaywrightTruthSocialClient",
        mock_client_type,
    )

    config = CollectorConfig(
        truth_social_username="realDonaldTrump",
        client_mode="playwright",
        collector_headless=False,
    )

    create_client(config)

    mock_client_type.assert_called_once_with("realDonaldTrump", headless=False)
