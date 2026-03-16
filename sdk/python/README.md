# mdc-sdk — Python SDK for Market Data Collector

**Version:** 0.1.0
**Last Updated:** 2026-03-16
**Audience:** Python developers, quant researchers, data engineers

A lightweight Python SDK for [Market Data Collector (MDC)](https://github.com/rodoHasArrived/Market-Data-Collector) that wraps the MDC HTTP REST API and provides:

- `snap(symbol, n)` — last *n* trades as a **pandas DataFrame**
- `history(symbol, from_date, to_date)` — historical records as a **pandas DataFrame**
- `quotes(symbol)` — current best bid/offer snapshot
- `orderbook(symbol, levels)` — L2 order book snapshot
- `status()` / `health()` — service diagnostics
- `live(symbol, interval)` — **async generator** streaming new ticks as DataFrame batches

---

## Requirements

- Python ≥ 3.10
- A running MDC instance (default: `http://localhost:8080`)

---

## Installation

```bash
# From the repository root
pip install sdk/python/

# Or in editable / development mode
pip install -e "sdk/python/[dev]"
```

---

## Quick start

### Synchronous

```python
from mdc import MDCClient

client = MDCClient("http://localhost:8080")

# Last 50 trades for AAPL → DataFrame
df = client.snap("AAPL")
print(df[["timestamp", "price", "size", "aggressor"]].tail())

# Historical data for a date range
hist = client.history("AAPL", from_date="2024-01-01", to_date="2024-01-31")
print(hist.head())

# Current best bid/offer
quote = client.quotes("AAPL")
print(f"Bid: {quote.bid_price} × {quote.bid_size}  Ask: {quote.ask_price} × {quote.ask_size}")

# L2 order book (top 5 levels)
ob = client.orderbook("AAPL", levels=5)
print("Bids:", [(lvl.price, lvl.size) for lvl in ob.bids])

# Service status
info = client.status()
print(f"Connected: {info.is_connected}, EPS: {info.events_per_second:.1f}")
```

### Asynchronous

```python
import asyncio
from mdc import AsyncMDCClient

async def main():
    async with AsyncMDCClient("http://localhost:8080") as client:
        # Same one-shot methods, all async
        df = await client.snap("AAPL", n=20)
        print(df)

        # Async iterator over live feed (polls every second)
        async for batch in client.live("AAPL", interval=1.0):
            print(f"New ticks: {len(batch)}")
            print(batch[["price", "size"]].to_string(index=False))

asyncio.run(main())
```

### Live feed with a timeout

```python
import asyncio
from mdc import AsyncMDCClient

async def stream_for_seconds(symbol: str, duration: float) -> None:
    async with AsyncMDCClient("http://localhost:8080") as client:
        try:
            async with asyncio.timeout(duration):
                async for batch in client.live(symbol, interval=0.5):
                    print(batch[["timestamp", "price", "size"]].to_string(index=False))
        except TimeoutError:
            print("Stream finished.")

asyncio.run(stream_for_seconds("SPY", duration=10.0))
```

---

## API reference

### `MDCClient(base_url, timeout=10.0)`

Synchronous client. All methods return immediately.

| Method | Returns |
|--------|---------|
| `snap(symbol, n=50)` | `pd.DataFrame` |
| `history(symbol, from_date, to_date, data_type, skip, limit)` | `pd.DataFrame` |
| `quotes(symbol)` | `Quote` |
| `orderbook(symbol, levels=10)` | `OrderBook` |
| `status()` | `StatusInfo` |
| `health()` | `dict` |

### `AsyncMDCClient(base_url, timeout=10.0)`

Asynchronous client. Use as `async with AsyncMDCClient(...) as client`.
All `MDCClient` methods are available as `async` coroutines, plus:

| Method | Returns |
|--------|---------|
| `live(symbol, interval=1.0, limit=100)` | `AsyncGenerator[pd.DataFrame, None]` |

### DataFrame columns — `snap()` / `live()`

| Column | Type | Description |
|--------|------|-------------|
| `symbol` | str | Ticker |
| `timestamp` | datetime64[UTC] | Trade timestamp |
| `price` | float64 | Executed price |
| `size` | int64 | Trade size |
| `aggressor` | str | `BUY`, `SELL`, or `UNKNOWN` |
| `sequence_number` | int64 | Feed sequence number |
| `stream_id` | str / None | Internal stream ID |
| `venue` | str / None | Exchange / venue |

### DataFrame columns — `history()`

| Column | Type | Description |
|--------|------|-------------|
| `timestamp` | datetime64[UTC] | Record timestamp |
| `symbol` | str | Ticker |
| `event_type` | str | Event category (e.g. `"trade"`) |
| `raw_json` | str | Raw JSON payload from the JSONL file |
| `source_file` | str | Source JSONL file path |
| `line_number` | int64 | Line number within the source file |

### Exceptions

All exceptions inherit from `mdc.MDCError`.

| Exception | When raised |
|-----------|-------------|
| `MDCConnectionError` | MDC server is unreachable |
| `MDCTimeoutError` | Request timed out |
| `MDCResponseError` | Server returned a non-2xx HTTP status |

---

## Running the tests

```bash
pip install -e "sdk/python/[dev]"
pytest sdk/python/tests/ -v
```

---

## How `live()` works

`live()` is a **polling-based** async generator — it calls
`GET /api/data/trades/{symbol}?limit={limit}` every *interval* seconds and
yields only ticks with a `sequence_number` greater than the last seen value.
Empty polls are silently skipped. Stop the stream with `break` or
`asyncio.timeout()`.
