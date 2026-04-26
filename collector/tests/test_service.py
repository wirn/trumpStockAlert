from pathlib import Path
from datetime import UTC, datetime
from typing import Any

from collector.normalizer import PostNormalizer
from collector.service import CollectorService
from collector.truth_post_store import TruthPostStore


class FakeClient:
    def __init__(self, posts: list[dict[str, Any]]) -> None:
        self.posts = posts

    def fetch_latest_posts(
        self, max_posts: int, created_after: datetime | None = None
    ) -> list[dict[str, Any]]:
        return self.posts[:max_posts]


def test_duplicate_filtering(tmp_path: Path) -> None:
    post_store = TruthPostStore(tmp_path / "truth-posts.json")
    post_store.append_new_posts(
        [
            PostNormalizer("realDonaldTrump").normalize(
                {
                    "id": "1",
                    "content": "Already seen",
                    "created_at": "2026-04-26T12:00:00.000Z",
                }
            )
        ]
    )
    service = CollectorService(
        client=FakeClient(
            [
                {
                    "id": "1",
                    "content": "Already seen",
                    "created_at": "2026-04-26T12:00:00.000Z",
                },
                {
                    "id": "2",
                    "content": "New post",
                    "created_at": "2026-04-26T12:01:00.000Z",
                },
            ]
        ),
        normalizer=PostNormalizer("realDonaldTrump"),
        post_store=post_store,
    )

    new_posts = service.run(max_posts=10)

    assert [post.externalId for post in new_posts] == ["2"]
    assert [post["externalId"] for post in post_store.load_posts()] == ["1", "2"]


def test_filters_out_posts_older_than_created_after(tmp_path: Path) -> None:
    post_store = TruthPostStore(tmp_path / "truth-posts.json")
    service = CollectorService(
        client=FakeClient(
            [
                {
                    "id": "1",
                    "content": "Too old",
                    "created_at": "2026-04-26T11:54:59+00:00",
                },
                {
                    "id": "2",
                    "content": "Recent enough",
                    "created_at": "2026-04-26T11:55:00+00:00",
                },
            ]
        ),
        normalizer=PostNormalizer("realDonaldTrump"),
        post_store=post_store,
    )

    new_posts = service.run(
        max_posts=10,
        created_after=datetime(2026, 4, 26, 11, 55, tzinfo=UTC),
    )

    assert [post.externalId for post in new_posts] == ["2"]
    assert [post["externalId"] for post in post_store.load_posts()] == ["2"]
