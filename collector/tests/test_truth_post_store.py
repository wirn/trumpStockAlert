from pathlib import Path

from collector.models import NormalizedPost
from collector.truth_post_store import TruthPostStore


def make_post(
    external_id: str,
    source: str = "truthsocial",
    content: str = "Post text",
) -> NormalizedPost:
    return NormalizedPost(
        source=source,
        author="realDonaldTrump",
        externalId=external_id,
        url=f"https://truthsocial.com/@realDonaldTrump/posts/{external_id}",
        content=content,
        createdAt="2026-04-26T12:00:00+00:00",
        collectedAt="2026-04-26T12:01:00+00:00",
        raw={"id": external_id},
    )


def test_missing_store_returns_empty_list(tmp_path: Path) -> None:
    store = TruthPostStore(tmp_path / "truth-posts.json")

    assert store.load_posts() == []


def test_existing_external_id_is_not_saved_twice_for_same_source(
    tmp_path: Path,
) -> None:
    store = TruthPostStore(tmp_path / "truth-posts.json")

    assert [post.externalId for post in store.append_new_posts([make_post("1")])] == [
        "1"
    ]
    assert store.append_new_posts([make_post("1")]) == []

    assert len(store.load_posts()) == 1


def test_new_post_is_appended(tmp_path: Path) -> None:
    store = TruthPostStore(tmp_path / "truth-posts.json")

    store.append_new_posts([make_post("1")])
    new_posts = store.append_new_posts([make_post("2")])

    assert [post.externalId for post in new_posts] == ["2"]
    assert [post["externalId"] for post in store.load_posts()] == ["1", "2"]


def test_duplicate_key_uses_source_and_external_id(tmp_path: Path) -> None:
    store = TruthPostStore(tmp_path / "truth-posts.json")

    new_posts = store.append_new_posts(
        [
            make_post("1", source="truthsocial"),
            make_post("1", source="other-source"),
        ]
    )

    assert [(post.source, post.externalId) for post in new_posts] == [
        ("truthsocial", "1"),
        ("other-source", "1"),
    ]
    assert len(store.load_posts()) == 2
