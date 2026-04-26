"""Command-line entry point for the Collector."""

from __future__ import annotations

import logging
import sys
from argparse import ArgumentParser
from datetime import UTC, datetime, timedelta
from typing import Any

from collector.api_truth_post_store import ApiTruthPostStore
from collector.config import CollectorConfig
from collector.normalizer import PostNormalizer
from collector.service import CollectorService
from collector.truth_post_store import TruthPostStore
from collector.truth_social_client import TruthSocialClient


def configure_logging() -> None:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s [%(name)s] %(message)s",
    )


def parse_args(argv: list[str] | None = None) -> bool:
    parser = ArgumentParser(description="Run the trumpStockAlert Collector.")
    parser.add_argument(
        "--test",
        action="store_true",
        help="Fetch exactly the latest 1 post and skip the lookback time filter.",
    )
    return parser.parse_args(argv).test


def main(argv: list[str] | None = None) -> int:
    configure_logging()
    logger = logging.getLogger(__name__)

    try:
        test_mode = parse_args(argv)
        config = CollectorConfig.from_env()
        max_posts = 1 if test_mode else config.max_posts
        created_after = None
        if test_mode:
            logger.info("Collector running in test mode.")
        else:
            created_after = datetime.now(UTC) - timedelta(
                minutes=config.lookback_minutes
            )
            logger.info(
                "Collector running in normal mode with %s-minute UTC lookback.",
                config.lookback_minutes,
            )

        post_store: Any = (
            ApiTruthPostStore(config.truth_post_api_base_url)
            if config.store_mode == "api"
            else TruthPostStore(config.truth_posts_file_path)
        )

        if isinstance(post_store, ApiTruthPostStore):
            logger.info("Collector API base URL: %s", post_store.base_url)
            logger.info("Collector API endpoint: %s", post_store.endpoint_url)

        service = CollectorService(
            client=TruthSocialClient(config.truth_social_username),
            normalizer=PostNormalizer(config.truth_social_username),
            post_store=post_store,
            output_mode=config.output_mode,
            test_mode=test_mode,
        )
        service.run(max_posts, created_after=created_after)
    except Exception:
        logger.exception("Collector failed.")
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
