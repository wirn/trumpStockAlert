from typing import Any
from collector.api_truth_post_store import ApiTruthPostStore
from collector.models import NormalizedPost


def make_post(external_id: str) -> NormalizedPost:
    return NormalizedPost(
        source="truthsocial",
        author="realDonaldTrump",
        externalId=external_id,
        url=f"https://truthsocial.com/@realDonaldTrump/posts/{external_id}",
        content="Post text",
        createdAt="2026-04-26T12:00:00+00:00",
        collectedAt="2026-04-26T12:01:00+00:00",
        raw={"id": external_id},
    )


def test_api_store_counts_created_and_existing_posts() -> None:
    class FakeApiStore(ApiTruthPostStore):
        def _post(self, post: NormalizedPost) -> tuple[int, dict[str, Any]]:
            if post.externalId == "existing":
                return 200, {"id": 10}
            return 201, {"id": 11}

    result = FakeApiStore("http://localhost:5044").save_posts(
        [make_post("existing"), make_post("new")]
    )

    assert result.already_existing_count == 1
    assert [post.externalId for post in result.saved_posts] == ["new"]


def test_api_store_error_message_explains_https_localhost_5044() -> None:
    message = ApiTruthPostStore("https://localhost:5044")._connection_error_message(
        "wrong version number"
    )

    assert "http://localhost:5044" in message
    assert "not HTTPS" in message
