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


def test_uses_text_when_content_is_empty() -> None:
    raw_post = {
        "id": "789",
        "content": "<p></p>",
        "text": "Plain fallback text",
        "created_at": "2026-04-26T12:00:00.000Z",
    }

    normalized = PostNormalizer("realDonaldTrump").normalize(raw_post)

    assert normalized.content == "Plain fallback text"


def test_uses_card_title_when_content_is_missing() -> None:
    raw_post = {
        "id": "790",
        "card": {
            "title": "Card headline",
            "description": "Card description",
        },
        "created_at": "2026-04-26T12:00:00.000Z",
    }

    normalized = PostNormalizer("realDonaldTrump").normalize(raw_post)

    assert normalized.content == "Card headline"


def test_uses_quote_content_when_top_level_content_is_whitespace() -> None:
    raw_post = {
        "id": "791",
        "content": "   ",
        "quote": {
            "content": "<p>Quoted &amp; cleaned</p>",
        },
        "created_at": "2026-04-26T12:00:00.000Z",
    }

    normalized = PostNormalizer("realDonaldTrump").normalize(raw_post)

    assert normalized.content == "Quoted & cleaned"


def test_uses_safe_content_fallback_when_post_has_no_text() -> None:
    raw_post = {
        "id": "792",
        "content": "",
        "card": {},
        "quote": None,
        "reblog": None,
        "created_at": "2026-04-26T12:00:00.000Z",
    }

    normalized = PostNormalizer("realDonaldTrump").normalize(raw_post)

    assert normalized.content == "[No text content]"
    assert normalized.content.strip()
