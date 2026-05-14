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


def test_uses_spoiler_text_when_content_is_empty() -> None:
    raw_post = {
        "id": "793",
        "content": "<p></p>",
        "spoiler_text": "Spoiler fallback &amp; summary",
        "created_at": "2026-04-26T12:00:00.000Z",
    }

    normalized = PostNormalizer("realDonaldTrump").normalize(raw_post)

    assert normalized.content == "Spoiler fallback & summary"


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


def test_uses_reblog_spoiler_text_when_reblog_content_is_empty() -> None:
    raw_post = {
        "id": "794",
        "content": "",
        "reblog": {
            "content": "",
            "spoiler_text": "Boosted post summary",
        },
        "created_at": "2026-04-26T12:00:00.000Z",
    }

    normalized = PostNormalizer("realDonaldTrump").normalize(raw_post)

    assert normalized.content == "Boosted post summary"


def test_uses_media_description_when_post_text_is_empty() -> None:
    raw_post = {
        "id": "795",
        "content": "",
        "media_attachments": [
            {
                "type": "image",
                "description": "Chart showing tariff impacts &amp; market reaction",
            }
        ],
        "created_at": "2026-04-26T12:00:00.000Z",
    }

    normalized = PostNormalizer("realDonaldTrump").normalize(raw_post)

    assert normalized.content == "Chart showing tariff impacts & market reaction"


def test_decodes_entities_before_stripping_html() -> None:
    raw_post = {
        "id": "796",
        "content": "&lt;p&gt;Encoded HTML &amp;amp; readable text&lt;/p&gt;",
        "created_at": "2026-04-26T12:00:00.000Z",
    }

    normalized = PostNormalizer("realDonaldTrump").normalize(raw_post)

    assert normalized.content == "Encoded HTML & readable text"


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
