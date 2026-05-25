"""Command-line entry point for the Collector."""

from __future__ import annotations

import logging
import sys
from argparse import ArgumentParser, Namespace
from datetime import UTC, datetime, timedelta
from typing import Any

from collector.api_truth_post_store import ApiTruthPostStore
from collector.client_factory import create_client
from collector.config import CollectorConfig
from collector.fetcher_run_reporter import FetcherRunReporter
from collector.normalizer import PostNormalizer
from collector.service import CollectorService
from collector.truth_post_store import TruthPostStore


def configure_logging() -> None:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s [%(name)s] %(message)s",
    )


def parse_args(argv: list[str] | None = None) -> Namespace:
    parser = ArgumentParser(description="Run the trumpStockAlert Collector.")
    parser.add_argument(
        "--test",
        action="store_true",
        help="Fetch exactly the latest 1 post and skip the lookback time filter.",
    )
    parser.add_argument(
        "--skip-lookback",
        action="store_true",
        help="Fetch the latest configured number of posts without applying the lookback time filter.",
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    configure_logging()
    logger = logging.getLogger(__name__)
    started_at = datetime.now(UTC)
    reporter: FetcherRunReporter | None = None

    # --- Configuration (fail fast before attempting any network calls) ---
    try:
        args = parse_args(argv)
        config = CollectorConfig.from_env()
    except Exception:
        logger.exception("Collector failed to start: could not load configuration.")
        return 1

    if config.store_mode == "api" and not config.scheduler_api_key:
        logger.error(
            "SCHEDULER_API_KEY must be set when COLLECTOR_STORE_MODE=api. "
            "Fetcher run logging requires authentication to the backend."
        )
        return 1

    test_mode = args.test

    if config.store_mode == "api":
        reporter = FetcherRunReporter(
            base_url=config.truth_post_api_base_url,
            scheduler_api_key=config.scheduler_api_key,
        )

    # --- Collector run ---
    try:
        max_posts = 1 if test_mode else config.max_posts
        created_after = None
        if test_mode:
            logger.info("Collector running in test mode.")
        elif args.skip_lookback:
            logger.info(
                "Collector running in normal mode without UTC lookback. Fetching latest %s post(s).",
                max_posts,
            )
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

        if config.store_mode == "api":
            logger.info("Collector API base URL: %s", config.truth_post_api_base_url)
            logger.info(
                "Collector API endpoint: %s/api/truth-posts",
                config.truth_post_api_base_url,
            )

        logger.info("Using collector client: %s.", config.client_mode)
        service = CollectorService(
            client=create_client(config),
            normalizer=PostNormalizer(config.truth_social_username),
            post_store=post_store,
            output_mode=config.output_mode,
            test_mode=test_mode,
        )
        summary = service.run(max_posts, created_after=created_after)

    except Exception:
        logger.exception("Collector failed.")
        if reporter is not None:
            try:
                reporter.report_failure(
                    started_at=started_at,
                    message="Collector failed with an unhandled exception.",
                )
            except Exception as report_exc:
                logger.error(
                    "Failed to report collector failure to backend: %s", report_exc
                )
        return 1

    # --- Report result (required; failure to report exits non-zero) ---
    if reporter is not None:
        try:
            reporter.report_run(
                started_at=summary.started_at,
                finished_at=summary.finished_at,
                success=summary.success,
                fetched_count=summary.fetched_count,
                inserted_count=summary.saved_count,
                duplicate_count=summary.already_existing_count,
                error_count=summary.failed_count,
                message=summary.message,
            )
        except Exception as exc:
            logger.error("FetcherRun reporting failed: %s", exc)
            return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
