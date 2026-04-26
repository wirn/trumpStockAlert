from collector.normalizer import PostNormalizer


def test_normalizes_truthbrush_post() -> None:
    raw_post = {
        "id": "123",
        "url": "https://truthsocial.com/@realDonaldTrump/posts/123",
        "content": "<p>Hello &amp; market watchers</p>",
        "created_at": "2026-04-26T12:00:00.000Z",
    }

    normalized = PostNormalizer("realDonaldTrump").normalize(raw_post)

    assert normalized.to_dict() == {
        "source": "truthsocial",
        "author": "realDonaldTrump",
        "externalId": "123",
        "url": "https://truthsocial.com/@realDonaldTrump/posts/123",
        "content": "Hello & market watchers",
        "createdAt": "2026-04-26T12:00:00.000Z",
        "collectedAt": normalized.collectedAt,
        "raw": raw_post,
    }


def test_uses_fallback_url_when_missing() -> None:
    raw_post = {
        "id": "456",
        "content": "Plain text",
        "created_at": "2026-04-26T12:00:00.000Z",
    }

    normalized = PostNormalizer("@realDonaldTrump").normalize(raw_post)

    assert normalized.url == "https://truthsocial.com/@realDonaldTrump/posts/456"
