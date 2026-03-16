"""Response model dataclasses for the MDC Python SDK."""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime
from typing import Any


@dataclass(frozen=True)
class Trade:
    """A single trade tick returned by the MDC trades endpoint."""

    symbol: str
    timestamp: datetime
    price: float
    size: int
    aggressor: str
    sequence_number: int
    stream_id: str | None = None
    venue: str | None = None

    @classmethod
    def from_dict(cls, d: dict[str, Any]) -> "Trade":
        return cls(
            symbol=d["symbol"],
            timestamp=datetime.fromisoformat(d["timestamp"].replace("Z", "+00:00")),
            price=float(d["price"]),
            size=int(d["size"]),
            aggressor=d.get("aggressor", "UNKNOWN"),
            sequence_number=int(d.get("sequenceNumber", 0)),
            stream_id=d.get("streamId"),
            venue=d.get("venue"),
        )


@dataclass(frozen=True)
class Quote:
    """Best bid/offer (BBO) snapshot."""

    symbol: str
    timestamp: datetime
    bid_price: float
    bid_size: int
    ask_price: float
    ask_size: int
    mid_price: float | None = None
    spread: float | None = None
    sequence_number: int = 0
    stream_id: str | None = None
    venue: str | None = None

    @classmethod
    def from_dict(cls, d: dict[str, Any]) -> "Quote":
        return cls(
            symbol=d["symbol"],
            timestamp=datetime.fromisoformat(d["timestamp"].replace("Z", "+00:00")),
            bid_price=float(d["bidPrice"]),
            bid_size=int(d["bidSize"]),
            ask_price=float(d["askPrice"]),
            ask_size=int(d["askSize"]),
            mid_price=float(d["midPrice"]) if d.get("midPrice") is not None else None,
            spread=float(d["spread"]) if d.get("spread") is not None else None,
            sequence_number=int(d.get("sequenceNumber", 0)),
            stream_id=d.get("streamId"),
            venue=d.get("venue"),
        )


@dataclass(frozen=True)
class OrderBookLevel:
    """A single price level in an order book."""

    side: str
    level: int
    price: float
    size: float
    market_maker: str | None = None

    @classmethod
    def from_dict(cls, d: dict[str, Any]) -> "OrderBookLevel":
        return cls(
            side=d["side"],
            level=int(d["level"]),
            price=float(d["price"]),
            size=float(d["size"]),
            market_maker=d.get("marketMaker"),
        )


@dataclass
class OrderBook:
    """Full order book snapshot."""

    symbol: str
    timestamp: datetime
    bids: list[OrderBookLevel] = field(default_factory=list)
    asks: list[OrderBookLevel] = field(default_factory=list)
    mid_price: float | None = None
    imbalance: float | None = None
    market_state: str = ""
    sequence_number: int = 0
    is_stale: bool = False
    stream_id: str | None = None
    venue: str | None = None

    @classmethod
    def from_dict(cls, d: dict[str, Any]) -> "OrderBook":
        return cls(
            symbol=d["symbol"],
            timestamp=datetime.fromisoformat(d["timestamp"].replace("Z", "+00:00")),
            bids=[OrderBookLevel.from_dict(b) for b in d.get("bids", [])],
            asks=[OrderBookLevel.from_dict(a) for a in d.get("asks", [])],
            mid_price=float(d["midPrice"]) if d.get("midPrice") is not None else None,
            imbalance=float(d["imbalance"]) if d.get("imbalance") is not None else None,
            market_state=d.get("marketState", ""),
            sequence_number=int(d.get("sequenceNumber", 0)),
            is_stale=bool(d.get("isStale", False)),
            stream_id=d.get("streamId"),
            venue=d.get("venue"),
        )


@dataclass(frozen=True)
class StatusInfo:
    """Summary of MDC service status."""

    is_connected: bool
    timestamp_utc: datetime
    uptime_seconds: float
    events_per_second: float
    source_provider: str
    published: int = 0
    dropped: int = 0

    @classmethod
    def from_dict(cls, d: dict[str, Any]) -> "StatusInfo":
        metrics = d.get("metrics", {})
        uptime_raw = d.get("uptime", "00:00:00")
        uptime_seconds: float
        if isinstance(uptime_raw, (int, float)):
            uptime_seconds = float(uptime_raw)
        else:
            try:
                parts = str(uptime_raw).split(":")
                uptime_seconds = (
                    float(parts[0]) * 3600 + float(parts[1]) * 60 + float(parts[2])
                    if len(parts) == 3
                    else 0.0
                )
            except (IndexError, ValueError):
                uptime_seconds = 0.0

        return cls(
            is_connected=bool(d.get("isConnected", False)),
            timestamp_utc=datetime.fromisoformat(
                d.get("timestampUtc", "1970-01-01T00:00:00Z").replace("Z", "+00:00")
            ),
            uptime_seconds=uptime_seconds,
            events_per_second=float(metrics.get("eventsPerSecond", 0.0)),
            source_provider=str(metrics.get("sourceProvider", "")),
            published=int(metrics.get("published", 0)),
            dropped=int(metrics.get("dropped", 0)),
        )
