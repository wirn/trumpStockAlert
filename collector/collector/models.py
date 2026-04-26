"""Data models used by the Collector."""

from __future__ import annotations

from dataclasses import asdict, dataclass
from typing import Any


@dataclass(frozen=True)
class NormalizedPost:
    source: str
    author: str
    externalId: str
    url: str
    content: str
    createdAt: str
    collectedAt: str
    raw: dict[str, Any]

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)
