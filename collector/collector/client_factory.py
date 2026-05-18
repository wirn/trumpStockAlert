"""Factory that creates the correct Truth Social client based on config."""

from __future__ import annotations

from typing import TYPE_CHECKING, Any

if TYPE_CHECKING:
    from collector.config import CollectorConfig


def create_client(config: CollectorConfig) -> Any:
    """Return a TruthbrushClient or PlaywrightTruthSocialClient based on COLLECTOR_CLIENT_MODE."""
    if config.client_mode == "playwright":
        from collector.playwright_client import PlaywrightTruthSocialClient

        return PlaywrightTruthSocialClient(config.truth_social_username)

    from collector.truth_social_client import TruthbrushClient

    return TruthbrushClient(config.truth_social_username)
