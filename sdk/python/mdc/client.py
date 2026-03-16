"""MDC Python SDK — synchronous and asynchronous client implementations."""

from __future__ import annotations

import asyncio
from collections.abc import AsyncGenerator
from datetime import date
from typing import Any

import httpx
import pandas as pd

from mdc._exceptions import MDCConnectionError, MDCResponseError, MDCTimeoutError
from mdc.models import OrderBook, Quote, StatusInfo

__all__ = ["MDCClient", "AsyncMDCClient"]

_TRADES_COLUMNS = [
    "symbol", "timestamp", "price", "size",
    "aggressor", "sequence_number", "stream_id", "venue",
]

_HISTORICAL_COLUMNS = [
    "timestamp", "symbol", "event_type", "raw_json", "source_file", "line_number",
]


def _raise_for_status(response: httpx.Response) -> None:
    if response.is_success:
        return
    try:
        body = response.json()
        message = body.get("error") or body.get("message") or response.text
    except Exception:
        message = response.text
    raise MDCResponseError(response.status_code, message)


def _trades_to_df(data: dict[str, Any]) -> pd.DataFrame:
    trades = data.get("trades", [])
    if not trades:
        return pd.DataFrame(columns=_TRADES_COLUMNS)
    rows = [
        {
            "symbol": t.get("symbol", data.get("symbol", "")),
            "timestamp": pd.Timestamp(t["timestamp"]),
            "price": float(t["price"]),
            "size": int(t["size"]),
            "aggressor": t.get("aggressor", "UNKNOWN"),
            "sequence_number": int(t.get("sequenceNumber", 0)),
            "stream_id": t.get("streamId"),
            "venue": t.get("venue"),
        }
        for t in trades
    ]
    df = pd.DataFrame(rows, columns=_TRADES_COLUMNS)
    df["timestamp"] = pd.to_datetime(df["timestamp"], utc=True)
    return df


def _historical_to_df(data: dict[str, Any]) -> pd.DataFrame:
    records = data.get("records", [])
    if not records:
        return pd.DataFrame(columns=_HISTORICAL_COLUMNS)
    rows = [
        {
            "timestamp": pd.Timestamp(r["timestamp"]),
            "symbol": r.get("symbol", data.get("symbol", "")),
            "event_type": r.get("eventType", ""),
            "raw_json": r.get("rawJson", ""),
            "source_file": r.get("sourceFile", ""),
            "line_number": int(r.get("lineNumber", 0)),
        }
        for r in records
    ]
    df = pd.DataFrame(rows, columns=_HISTORICAL_COLUMNS)
    df["timestamp"] = pd.to_datetime(df["timestamp"], utc=True)
    return df


class MDCClient:
    """Synchronous HTTP client for the Market Data Collector REST API.

    Parameters
    ----------
    base_url:
        Base URL of the running MDC instance, e.g. ``"http://localhost:8080"``.
    timeout:
        Default request timeout in seconds (default: ``10.0``).

    Examples
    --------
    >>> client = MDCClient("http://localhost:8080")
    >>> df = client.snap("AAPL", n=20)
    >>> df = client.history("AAPL", from_date="2024-01-01", to_date="2024-01-31")
    """

    def __init__(self, base_url: str, timeout: float = 10.0, **httpx_kwargs: Any) -> None:
        self._base_url = base_url.rstrip("/")
        self._timeout = timeout
        self._httpx_kwargs = httpx_kwargs

    def __enter__(self) -> "MDCClient":
        return self

    def __exit__(self, *_: Any) -> None:
        pass

    def _get(self, path: str, **params: Any) -> dict[str, Any]:
        filtered = {k: v for k, v in params.items() if v is not None}
        try:
            with httpx.Client(
                base_url=self._base_url, timeout=self._timeout, **self._httpx_kwargs
            ) as http:
                resp = http.get(path, params=filtered)
        except httpx.ConnectError as exc:
            raise MDCConnectionError(f"Cannot connect to MDC at {self._base_url}") from exc
        except httpx.TimeoutException as exc:
            raise MDCTimeoutError(f"Request timed out: {path}") from exc
        _raise_for_status(resp)
        return resp.json()

    def snap(self, symbol: str, n: int = 50) -> pd.DataFrame:
        """Return the last *n* trades for *symbol* as a :class:`pandas.DataFrame`.

        Parameters
        ----------
        symbol:
            Ticker symbol (case-insensitive).
        n:
            Maximum number of ticks to retrieve (default: ``50``).

        Returns
        -------
        pandas.DataFrame
            Columns: ``symbol``, ``timestamp``, ``price``, ``size``,
            ``aggressor``, ``sequence_number``, ``stream_id``, ``venue``.
        """
        data = self._get(f"/api/data/trades/{symbol.upper()}", limit=n)
        return _trades_to_df(data)

    def history(
        self,
        symbol: str,
        from_date: str | date | None = None,
        to_date: str | date | None = None,
        data_type: str | None = None,
        skip: int | None = None,
        limit: int | None = None,
    ) -> pd.DataFrame:
        """Retrieve historical records for *symbol* as a :class:`pandas.DataFrame`.

        Parameters
        ----------
        symbol:
            Ticker symbol (case-insensitive).
        from_date:
            Start date (inclusive) as ``"YYYY-MM-DD"`` string or :class:`datetime.date`.
        to_date:
            End date (inclusive) as ``"YYYY-MM-DD"`` string or :class:`datetime.date`.
        data_type:
            Filter by event type (e.g. ``"trades"``).  ``None`` returns all types.
        skip:
            Number of records to skip (for pagination).
        limit:
            Maximum number of records to return.

        Returns
        -------
        pandas.DataFrame
            Columns: ``timestamp``, ``symbol``, ``event_type``, ``raw_json``,
            ``source_file``, ``line_number``.
        """
        data = self._get(
            "/api/historical",
            symbol=symbol.upper(),
            **{
                "from": str(from_date) if from_date is not None else None,
                "to": str(to_date) if to_date is not None else None,
                "dataType": data_type,
                "skip": skip,
                "limit": limit,
            },
        )
        return _historical_to_df(data)

    def quotes(self, symbol: str) -> Quote:
        """Return the current best bid/offer for *symbol*.

        Parameters
        ----------
        symbol:
            Ticker symbol (case-insensitive).

        Returns
        -------
        Quote
            Latest BBO snapshot.
        """
        data = self._get(f"/api/data/quotes/{symbol.upper()}")
        return Quote.from_dict(data.get("quote") or data)

    def orderbook(self, symbol: str, levels: int = 10) -> OrderBook:
        """Return the current L2 order book for *symbol*.

        Parameters
        ----------
        symbol:
            Ticker symbol (case-insensitive).
        levels:
            Number of depth levels to retrieve (default: ``10``).

        Returns
        -------
        OrderBook
            Full order book snapshot including bids and asks.
        """
        data = self._get(f"/api/data/orderbook/{symbol.upper()}", levels=levels)
        return OrderBook.from_dict(data)

    def status(self) -> StatusInfo:
        """Return the current MDC service status.

        Returns
        -------
        StatusInfo
            Connection state, throughput metrics and provider name.
        """
        return StatusInfo.from_dict(self._get("/api/status"))

    def health(self) -> dict[str, Any]:
        """Return the raw health-check response as a plain dict.

        Returns
        -------
        dict
            Raw JSON response from ``GET /health``.
        """
        return self._get("/health")


class AsyncMDCClient:
    """Asynchronous HTTP client for the Market Data Collector REST API.

    Parameters
    ----------
    base_url:
        Base URL of the running MDC instance.
    timeout:
        Default request timeout in seconds (default: ``10.0``).

    Examples
    --------
    >>> async with AsyncMDCClient("http://localhost:8080") as client:
    ...     df = await client.snap("AAPL")
    ...     async for batch in client.live("AAPL", interval=1.0):
    ...         process(batch)
    """

    def __init__(self, base_url: str, timeout: float = 10.0, **httpx_kwargs: Any) -> None:
        self._base_url = base_url.rstrip("/")
        self._timeout = timeout
        self._httpx_kwargs = httpx_kwargs
        self._http: httpx.AsyncClient | None = None

    async def __aenter__(self) -> "AsyncMDCClient":
        self._http = httpx.AsyncClient(
            base_url=self._base_url, timeout=self._timeout, **self._httpx_kwargs
        )
        return self

    async def __aexit__(self, *_: Any) -> None:
        if self._http is not None:
            await self._http.aclose()
            self._http = None

    def _client(self) -> httpx.AsyncClient:
        if self._http is None:
            self._http = httpx.AsyncClient(
                base_url=self._base_url, timeout=self._timeout, **self._httpx_kwargs
            )
        return self._http

    async def _get(self, path: str, **params: Any) -> dict[str, Any]:
        filtered = {k: v for k, v in params.items() if v is not None}
        try:
            resp = await self._client().get(path, params=filtered)
        except httpx.ConnectError as exc:
            raise MDCConnectionError(f"Cannot connect to MDC at {self._base_url}") from exc
        except httpx.TimeoutException as exc:
            raise MDCTimeoutError(f"Request timed out: {path}") from exc
        _raise_for_status(resp)
        return resp.json()

    async def snap(self, symbol: str, n: int = 50) -> pd.DataFrame:
        """Return the last *n* trades for *symbol* as a :class:`pandas.DataFrame`.

        Parameters
        ----------
        symbol:
            Ticker symbol (case-insensitive).
        n:
            Maximum number of ticks to retrieve (default: ``50``).

        Returns
        -------
        pandas.DataFrame
            Columns: ``symbol``, ``timestamp``, ``price``, ``size``,
            ``aggressor``, ``sequence_number``, ``stream_id``, ``venue``.
        """
        data = await self._get(f"/api/data/trades/{symbol.upper()}", limit=n)
        return _trades_to_df(data)

    async def history(
        self,
        symbol: str,
        from_date: str | date | None = None,
        to_date: str | date | None = None,
        data_type: str | None = None,
        skip: int | None = None,
        limit: int | None = None,
    ) -> pd.DataFrame:
        """Retrieve historical records for *symbol* as a :class:`pandas.DataFrame`.

        Parameters
        ----------
        symbol:
            Ticker symbol (case-insensitive).
        from_date:
            Start date as ``"YYYY-MM-DD"`` string or :class:`datetime.date`.
        to_date:
            End date as ``"YYYY-MM-DD"`` string or :class:`datetime.date`.
        data_type:
            Filter by event type.  ``None`` returns all types.
        skip:
            Number of records to skip (pagination).
        limit:
            Maximum number of records to return.

        Returns
        -------
        pandas.DataFrame
            Columns: ``timestamp``, ``symbol``, ``event_type``, ``raw_json``,
            ``source_file``, ``line_number``.
        """
        data = await self._get(
            "/api/historical",
            symbol=symbol.upper(),
            **{
                "from": str(from_date) if from_date is not None else None,
                "to": str(to_date) if to_date is not None else None,
                "dataType": data_type,
                "skip": skip,
                "limit": limit,
            },
        )
        return _historical_to_df(data)

    async def quotes(self, symbol: str) -> Quote:
        """Return the current best bid/offer for *symbol*.

        Parameters
        ----------
        symbol:
            Ticker symbol (case-insensitive).

        Returns
        -------
        Quote
            Latest BBO snapshot.
        """
        data = await self._get(f"/api/data/quotes/{symbol.upper()}")
        return Quote.from_dict(data.get("quote") or data)

    async def orderbook(self, symbol: str, levels: int = 10) -> OrderBook:
        """Return the current L2 order book for *symbol*.

        Parameters
        ----------
        symbol:
            Ticker symbol (case-insensitive).
        levels:
            Number of depth levels to retrieve (default: ``10``).

        Returns
        -------
        OrderBook
            Full order book snapshot.
        """
        data = await self._get(f"/api/data/orderbook/{symbol.upper()}", levels=levels)
        return OrderBook.from_dict(data)

    async def status(self) -> StatusInfo:
        """Return the current MDC service status.

        Returns
        -------
        StatusInfo
            Connection state, throughput metrics and provider name.
        """
        return StatusInfo.from_dict(await self._get("/api/status"))

    async def health(self) -> dict[str, Any]:
        """Return the raw health-check response as a plain dict.

        Returns
        -------
        dict
            Raw JSON response from ``GET /health``.
        """
        return await self._get("/health")

    async def live(
        self,
        symbol: str,
        interval: float = 1.0,
        limit: int = 100,
    ) -> AsyncGenerator[pd.DataFrame, None]:
        """Async generator that yields new trade ticks as :class:`pandas.DataFrame` batches.

        Polls the MDC trades endpoint every *interval* seconds and emits only
        ticks that have a sequence number higher than the last seen value.
        Empty polls are silently skipped.  The generator runs indefinitely;
        use ``break`` or :func:`asyncio.timeout` to stop it.

        Parameters
        ----------
        symbol:
            Ticker symbol (case-insensitive).
        interval:
            Polling interval in seconds (default: ``1.0``).
        limit:
            Number of ticks to request per poll (default: ``100``).

        Yields
        ------
        pandas.DataFrame
            Batches of **new** trades (never empty).

        Examples
        --------
        >>> async with AsyncMDCClient("http://localhost:8080") as client:
        ...     async for batch in client.live("AAPL", interval=0.5):
        ...         print(batch[["price", "size"]].tail())
        """
        last_seq: int = -1
        sym = symbol.upper()
        while True:
            data = await self._get(f"/api/data/trades/{sym}", limit=limit)
            df = _trades_to_df(data)
            if not df.empty:
                new_ticks = df[df["sequence_number"] > last_seq]
                if not new_ticks.empty:
                    last_seq = int(new_ticks["sequence_number"].max())
                    yield new_ticks.reset_index(drop=True)
            await asyncio.sleep(interval)
