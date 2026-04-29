import logging
from io import BytesIO
from typing import Any
from urllib.error import HTTPError

from collector.api_truth_post_store import ApiTruthPostStore, BackendPostResponse
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
        def _post(self, post: NormalizedPost) -> BackendPostResponse:
            if post.externalId == "existing":
                return BackendPostResponse(200, '{"id": 10}', {"id": 10})
            return BackendPostResponse(201, '{"id": 11}', {"id": 11})

    result = FakeApiStore("http://localhost:5044").save_posts(
        [make_post("existing"), make_post("new")]
    )

    assert result.already_existing_count == 1
    assert result.failed_count == 0
    assert [post.externalId for post in result.saved_posts] == ["new"]


def test_api_store_records_failed_post_and_continues(caplog: Any) -> None:
    class FakeApiStore(ApiTruthPostStore):
        def _post(self, post: NormalizedPost) -> BackendPostResponse:
            if post.externalId == "invalid":
                return BackendPostResponse(
                    400,
                    '{"errors":{"content":["Content is required."]}}',
                    {"errors": {"content": ["Content is required."]}},
                )
            return BackendPostResponse(201, '{"id": 11}', {"id": 11})

    with caplog.at_level(logging.ERROR):
        result = FakeApiStore("http://localhost:5044").save_posts(
            [make_post("invalid"), make_post("new")]
        )

    assert result.failed_count == 1
    assert result.already_existing_count == 0
    assert [post.externalId for post in result.saved_posts] == ["new"]
    assert "FailedPost ExternalId=invalid" in caplog.text
    assert "StatusCode=400" in caplog.text
    assert "Content is required" in caplog.text


def test_api_store_treats_duplicate_conflict_as_skipped() -> None:
    class FakeApiStore(ApiTruthPostStore):
        def _post(self, post: NormalizedPost) -> BackendPostResponse:
            return BackendPostResponse(
                409,
                '{"message":"Post already exists."}',
                {"message": "Post already exists."},
            )

    result = FakeApiStore("http://localhost:5044").save_posts([make_post("existing")])

    assert result.already_existing_count == 1
    assert result.failed_count == 0
    assert result.saved_posts == []


def test_http_error_returns_response_with_status_and_body(caplog: Any) -> None:
    class FakeBody(BytesIO):
        def read(self, *args: Any, **kwargs: Any) -> bytes:
            return b'{"error":"validation failed"}'

    class FakeApiStore(ApiTruthPostStore):
        def _open(self, *_args: Any, **_kwargs: Any) -> Any:
            raise HTTPError(
                url=self.endpoint_url,
                code=422,
                msg="Unprocessable Entity",
                hdrs={},
                fp=FakeBody(),
            )

    with caplog.at_level(logging.ERROR):
        response = FakeApiStore("http://localhost:5044")._post(make_post("bad"))

    assert response.status_code == 422
    assert response.body == '{"error":"validation failed"}'
    assert response.payload == {"error": "validation failed"}
    assert "FailedPost ExternalId=bad" in caplog.text
    assert "StatusCode=422" in caplog.text
    assert "validation failed" in caplog.text


def test_api_store_error_message_explains_https_localhost_5044() -> None:
    message = ApiTruthPostStore("https://localhost:5044")._connection_error_message(
        "wrong version number"
    )

    assert "http://localhost:5044" in message
    assert "not HTTPS" in message
