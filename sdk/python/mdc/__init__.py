"""
mdc — Python SDK for Market Data Collector.

Quick start::

    from mdc import MDCClient

    client = MDCClient("http://localhost:8080")
    df = client.snap("AAPL")            # last 50 trades → DataFrame
    df = client.history("AAPL", "2024-01-01", "2024-01-31")

Async usage::

    from mdc import AsyncMDCClient

    async with AsyncMDCClient("http://localhost:8080") as client:
        async for batch in client.live("AAPL", interval=1.0):
            print(batch)
"""

from mdc._exceptions import MDCConnectionError, MDCError, MDCResponseError, MDCTimeoutError
from mdc.client import AsyncMDCClient, MDCClient
from mdc.models import OrderBook, OrderBookLevel, Quote, StatusInfo, Trade

__version__ = "0.1.0"

__all__ = [
    "MDCClient",
    "AsyncMDCClient",
    "Trade",
    "Quote",
    "OrderBook",
    "OrderBookLevel",
    "StatusInfo",
    "MDCError",
    "MDCConnectionError",
    "MDCTimeoutError",
    "MDCResponseError",
    "__version__",
]
