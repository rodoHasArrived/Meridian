"""Shared pytest fixtures and sample payloads for MDC SDK tests."""

from __future__ import annotations

BASE_URL = "http://mdc-test.local"

TRADES_PAYLOAD = {
    "symbol": "AAPL",
    "count": 3,
    "timestamp": "2024-01-15T14:30:10Z",
    "trades": [
        {
            "symbol": "AAPL",
            "timestamp": "2024-01-15T14:30:01Z",
            "price": 180.10,
            "size": 100,
            "aggressor": "BUY",
            "sequenceNumber": 1001,
            "streamId": "stream-1",
            "venue": "NYSE",
        },
        {
            "symbol": "AAPL",
            "timestamp": "2024-01-15T14:30:05Z",
            "price": 180.20,
            "size": 200,
            "aggressor": "SELL",
            "sequenceNumber": 1002,
            "streamId": "stream-1",
            "venue": "NYSE",
        },
        {
            "symbol": "AAPL",
            "timestamp": "2024-01-15T14:30:09Z",
            "price": 180.15,
            "size": 50,
            "aggressor": "BUY",
            "sequenceNumber": 1003,
            "streamId": "stream-1",
            "venue": "NASDAQ",
        },
    ],
}

TRADES_PAYLOAD_UPDATED = {
    "symbol": "AAPL",
    "count": 2,
    "timestamp": "2024-01-15T14:30:15Z",
    "trades": [
        {
            "symbol": "AAPL",
            "timestamp": "2024-01-15T14:30:11Z",
            "price": 180.25,
            "size": 75,
            "aggressor": "BUY",
            "sequenceNumber": 1003,  # same as before — should be filtered out
            "streamId": "stream-1",
            "venue": "NYSE",
        },
        {
            "symbol": "AAPL",
            "timestamp": "2024-01-15T14:30:13Z",
            "price": 180.30,
            "size": 150,
            "aggressor": "SELL",
            "sequenceNumber": 1004,  # new tick
            "streamId": "stream-1",
            "venue": "NYSE",
        },
    ],
}

QUOTES_PAYLOAD = {
    "symbol": "AAPL",
    "timestamp": "2024-01-15T14:30:10Z",
    "quote": {
        "symbol": "AAPL",
        "timestamp": "2024-01-15T14:30:10Z",
        "bidPrice": 180.10,
        "bidSize": 500,
        "askPrice": 180.15,
        "askSize": 300,
        "midPrice": 180.125,
        "spread": 0.05,
        "sequenceNumber": 2001,
        "streamId": "stream-1",
        "venue": "NYSE",
    },
}

ORDERBOOK_PAYLOAD = {
    "symbol": "AAPL",
    "timestamp": "2024-01-15T14:30:10Z",
    "bids": [
        {"side": "BID", "level": 1, "price": 180.10, "size": 500.0, "marketMaker": None},
        {"side": "BID", "level": 2, "price": 180.05, "size": 1000.0, "marketMaker": None},
    ],
    "asks": [
        {"side": "ASK", "level": 1, "price": 180.15, "size": 300.0, "marketMaker": None},
        {"side": "ASK", "level": 2, "price": 180.20, "size": 800.0, "marketMaker": None},
    ],
    "midPrice": 180.125,
    "imbalance": 0.25,
    "marketState": "OPEN",
    "sequenceNumber": 3001,
    "isStale": False,
    "streamId": "stream-1",
    "venue": "NYSE",
}

STATUS_PAYLOAD = {
    "isConnected": True,
    "timestampUtc": "2024-01-15T14:30:10Z",
    "uptime": "01:23:45.678",
    "metrics": {
        "eventsPerSecond": 42.5,
        "sourceProvider": "Alpaca",
        "published": 100000,
        "dropped": 5,
    },
}

HEALTH_PAYLOAD = {
    "status": "Healthy",
    "timestamp": "2024-01-15T14:30:10Z",
    "uptime": "01:23:45",
    "checks": [
        {"name": "pipeline", "status": "Healthy", "message": "Pipeline running"},
        {"name": "storage", "status": "Healthy", "message": "Storage accessible"},
    ],
}

HISTORICAL_PAYLOAD = {
    "success": True,
    "symbol": "AAPL",
    "from": "2024-01-01",
    "to": "2024-01-31",
    "dataType": "trades",
    "totalRecords": 2,
    "filesProcessed": 1,
    "totalFiles": 1,
    "queryTimeMs": 12,
    "records": [
        {
            "sourceFile": "aapl_trades_2024-01-02.jsonl",
            "lineNumber": 1,
            "timestamp": "2024-01-02T14:30:00Z",
            "symbol": "AAPL",
            "eventType": "trade",
            "rawJson": '{"price":180.0,"size":100}',
        },
        {
            "sourceFile": "aapl_trades_2024-01-03.jsonl",
            "lineNumber": 1,
            "timestamp": "2024-01-03T14:30:00Z",
            "symbol": "AAPL",
            "eventType": "trade",
            "rawJson": '{"price":181.0,"size":200}',
        },
    ],
}

EMPTY_TRADES_PAYLOAD = {
    "symbol": "AAPL",
    "count": 0,
    "timestamp": "2024-01-15T14:30:10Z",
    "trades": [],
}
