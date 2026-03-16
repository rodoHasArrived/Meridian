"""Unit tests for MDCClient (sync) and AsyncMDCClient (async).

All network calls are intercepted with ``respx`` so no live MDC instance
is required.
"""

from __future__ import annotations

import pandas as pd
import pytest
import respx
from httpx import Response

from mdc import AsyncMDCClient, MDCClient
from mdc._exceptions import MDCConnectionError, MDCResponseError, MDCTimeoutError
from mdc.models import OrderBook, OrderBookLevel, Quote, StatusInfo
from tests.conftest import (
    BASE_URL,
    EMPTY_TRADES_PAYLOAD,
    HEALTH_PAYLOAD,
    HISTORICAL_PAYLOAD,
    ORDERBOOK_PAYLOAD,
    QUOTES_PAYLOAD,
    STATUS_PAYLOAD,
    TRADES_PAYLOAD,
    TRADES_PAYLOAD_UPDATED,
)


# ===========================================================================
# Synchronous client — snap()
# ===========================================================================


class TestMDCClientSnap:
    @respx.mock
    def test_snap_returns_dataframe(self) -> None:
        respx.get(f"{BASE_URL}/api/data/trades/AAPL").mock(
            return_value=Response(200, json=TRADES_PAYLOAD)
        )
        df = MDCClient(BASE_URL).snap("AAPL")
        assert isinstance(df, pd.DataFrame)
        assert len(df) == 3
        assert list(df.columns) == [
            "symbol", "timestamp", "price", "size",
            "aggressor", "sequence_number", "stream_id", "venue",
        ]

    @respx.mock
    def test_snap_normalises_symbol_to_uppercase(self) -> None:
        route = respx.get(f"{BASE_URL}/api/data/trades/AAPL").mock(
            return_value=Response(200, json=TRADES_PAYLOAD)
        )
        MDCClient(BASE_URL).snap("aapl")
        assert route.called

    @respx.mock
    def test_snap_passes_limit_parameter(self) -> None:
        route = respx.get(f"{BASE_URL}/api/data/trades/AAPL").mock(
            return_value=Response(200, json=TRADES_PAYLOAD)
        )
        MDCClient(BASE_URL).snap("AAPL", n=10)
        assert "limit=10" in str(route.calls.last.request.url)

    @respx.mock
    def test_snap_empty_response_returns_empty_dataframe(self) -> None:
        respx.get(f"{BASE_URL}/api/data/trades/AAPL").mock(
            return_value=Response(200, json=EMPTY_TRADES_PAYLOAD)
        )
        df = MDCClient(BASE_URL).snap("AAPL")
        assert isinstance(df, pd.DataFrame)
        assert df.empty

    @respx.mock
    def test_snap_timestamp_is_utc(self) -> None:
        respx.get(f"{BASE_URL}/api/data/trades/AAPL").mock(
            return_value=Response(200, json=TRADES_PAYLOAD)
        )
        df = MDCClient(BASE_URL).snap("AAPL")
        # pandas 2.x → datetime64[us, UTC]; pandas 1.x → datetime64[ns, UTC]
        assert "UTC" in str(df["timestamp"].dtype)

    @respx.mock
    def test_snap_raises_mdc_response_error_on_404(self) -> None:
        respx.get(f"{BASE_URL}/api/data/trades/UNKN").mock(
            return_value=Response(404, json={"error": "symbol not found"})
        )
        with pytest.raises(MDCResponseError) as exc_info:
            MDCClient(BASE_URL).snap("UNKN")
        assert exc_info.value.status_code == 404

    @respx.mock
    def test_snap_raises_mdc_response_error_on_500(self) -> None:
        respx.get(f"{BASE_URL}/api/data/trades/AAPL").mock(
            return_value=Response(500, text="Internal Server Error")
        )
        with pytest.raises(MDCResponseError) as exc_info:
            MDCClient(BASE_URL).snap("AAPL")
        assert exc_info.value.status_code == 500

    @respx.mock
    def test_snap_price_and_size_types(self) -> None:
        respx.get(f"{BASE_URL}/api/data/trades/AAPL").mock(
            return_value=Response(200, json=TRADES_PAYLOAD)
        )
        df = MDCClient(BASE_URL).snap("AAPL")
        assert df["price"].dtype == float
        assert df["size"].dtype in (int, "int64")


# ===========================================================================
# Synchronous client — history()
# ===========================================================================


class TestMDCClientHistory:
    @respx.mock
    def test_history_returns_dataframe(self) -> None:
        respx.get(f"{BASE_URL}/api/historical").mock(
            return_value=Response(200, json=HISTORICAL_PAYLOAD)
        )
        df = MDCClient(BASE_URL).history("AAPL", from_date="2024-01-01", to_date="2024-01-31")
        assert isinstance(df, pd.DataFrame)
        assert len(df) == 2
        assert "timestamp" in df.columns
        assert "raw_json" in df.columns

    @respx.mock
    def test_history_normalises_symbol_to_uppercase(self) -> None:
        route = respx.get(f"{BASE_URL}/api/historical").mock(
            return_value=Response(200, json=HISTORICAL_PAYLOAD)
        )
        MDCClient(BASE_URL).history("aapl")
        assert "symbol=AAPL" in str(route.calls.last.request.url)

    @respx.mock
    def test_history_passes_date_parameters(self) -> None:
        route = respx.get(f"{BASE_URL}/api/historical").mock(
            return_value=Response(200, json=HISTORICAL_PAYLOAD)
        )
        MDCClient(BASE_URL).history("AAPL", from_date="2024-01-01", to_date="2024-01-31")
        url = str(route.calls.last.request.url)
        assert "from=2024-01-01" in url
        assert "to=2024-01-31" in url

    @respx.mock
    def test_history_empty_records_returns_empty_dataframe(self) -> None:
        empty = {**HISTORICAL_PAYLOAD, "records": [], "totalRecords": 0}
        respx.get(f"{BASE_URL}/api/historical").mock(return_value=Response(200, json=empty))
        df = MDCClient(BASE_URL).history("AAPL")
        assert isinstance(df, pd.DataFrame)
        assert df.empty

    @respx.mock
    def test_history_timestamp_is_utc(self) -> None:
        respx.get(f"{BASE_URL}/api/historical").mock(
            return_value=Response(200, json=HISTORICAL_PAYLOAD)
        )
        df = MDCClient(BASE_URL).history("AAPL")
        assert "UTC" in str(df["timestamp"].dtype)


# ===========================================================================
# Synchronous client — quotes()
# ===========================================================================


class TestMDCClientQuotes:
    @respx.mock
    def test_quotes_returns_quote_object(self) -> None:
        respx.get(f"{BASE_URL}/api/data/quotes/AAPL").mock(
            return_value=Response(200, json=QUOTES_PAYLOAD)
        )
        quote = MDCClient(BASE_URL).quotes("AAPL")
        assert isinstance(quote, Quote)
        assert quote.symbol == "AAPL"
        assert quote.bid_price == 180.10
        assert quote.ask_price == 180.15
        assert quote.mid_price == 180.125
        assert quote.spread == 0.05

    @respx.mock
    def test_quotes_normalises_symbol_to_uppercase(self) -> None:
        route = respx.get(f"{BASE_URL}/api/data/quotes/AAPL").mock(
            return_value=Response(200, json=QUOTES_PAYLOAD)
        )
        MDCClient(BASE_URL).quotes("aapl")
        assert route.called


# ===========================================================================
# Synchronous client — orderbook()
# ===========================================================================


class TestMDCClientOrderBook:
    @respx.mock
    def test_orderbook_returns_orderbook_object(self) -> None:
        respx.get(f"{BASE_URL}/api/data/orderbook/AAPL").mock(
            return_value=Response(200, json=ORDERBOOK_PAYLOAD)
        )
        ob = MDCClient(BASE_URL).orderbook("AAPL")
        assert isinstance(ob, OrderBook)
        assert ob.symbol == "AAPL"
        assert len(ob.bids) == 2
        assert len(ob.asks) == 2
        assert ob.mid_price == 180.125
        assert ob.imbalance == 0.25
        assert ob.market_state == "OPEN"
        assert not ob.is_stale

    @respx.mock
    def test_orderbook_levels_parameter_passed(self) -> None:
        route = respx.get(f"{BASE_URL}/api/data/orderbook/AAPL").mock(
            return_value=Response(200, json=ORDERBOOK_PAYLOAD)
        )
        MDCClient(BASE_URL).orderbook("AAPL", levels=5)
        assert "levels=5" in str(route.calls.last.request.url)

    @respx.mock
    def test_orderbook_level_fields(self) -> None:
        respx.get(f"{BASE_URL}/api/data/orderbook/AAPL").mock(
            return_value=Response(200, json=ORDERBOOK_PAYLOAD)
        )
        ob = MDCClient(BASE_URL).orderbook("AAPL")
        best_bid = ob.bids[0]
        assert isinstance(best_bid, OrderBookLevel)
        assert best_bid.side == "BID"
        assert best_bid.level == 1
        assert best_bid.price == 180.10


# ===========================================================================
# Synchronous client — status() / health()
# ===========================================================================


class TestMDCClientStatus:
    @respx.mock
    def test_status_returns_status_info(self) -> None:
        respx.get(f"{BASE_URL}/api/status").mock(return_value=Response(200, json=STATUS_PAYLOAD))
        info = MDCClient(BASE_URL).status()
        assert isinstance(info, StatusInfo)
        assert info.is_connected is True
        assert info.source_provider == "Alpaca"
        assert info.events_per_second == 42.5
        assert info.published == 100000
        assert info.dropped == 5

    @respx.mock
    def test_status_uptime_parsed_from_timespan_string(self) -> None:
        respx.get(f"{BASE_URL}/api/status").mock(return_value=Response(200, json=STATUS_PAYLOAD))
        info = MDCClient(BASE_URL).status()
        # "01:23:45.678" → 1*3600 + 23*60 + 45.678 = 5025.678 seconds
        assert abs(info.uptime_seconds - 5025.678) < 1.0


class TestMDCClientHealth:
    @respx.mock
    def test_health_returns_dict(self) -> None:
        respx.get(f"{BASE_URL}/health").mock(return_value=Response(200, json=HEALTH_PAYLOAD))
        result = MDCClient(BASE_URL).health()
        assert isinstance(result, dict)
        assert result["status"] == "Healthy"

    @respx.mock
    def test_health_raises_mdc_response_error_on_503(self) -> None:
        respx.get(f"{BASE_URL}/health").mock(
            return_value=Response(503, json={"status": "Unhealthy"})
        )
        with pytest.raises(MDCResponseError) as exc_info:
            MDCClient(BASE_URL).health()
        assert exc_info.value.status_code == 503


# ===========================================================================
# Asynchronous client — snap / history / status
# ===========================================================================


class TestAsyncMDCClientSnap:
    @pytest.mark.asyncio
    @respx.mock
    async def test_snap_returns_dataframe(self) -> None:
        respx.get(f"{BASE_URL}/api/data/trades/AAPL").mock(
            return_value=Response(200, json=TRADES_PAYLOAD)
        )
        async with AsyncMDCClient(BASE_URL) as client:
            df = await client.snap("AAPL")
        assert isinstance(df, pd.DataFrame)
        assert len(df) == 3

    @pytest.mark.asyncio
    @respx.mock
    async def test_snap_normalises_symbol_to_uppercase(self) -> None:
        route = respx.get(f"{BASE_URL}/api/data/trades/AAPL").mock(
            return_value=Response(200, json=TRADES_PAYLOAD)
        )
        async with AsyncMDCClient(BASE_URL) as client:
            await client.snap("aapl")
        assert route.called

    @pytest.mark.asyncio
    @respx.mock
    async def test_snap_raises_mdc_response_error_on_404(self) -> None:
        respx.get(f"{BASE_URL}/api/data/trades/UNKN").mock(
            return_value=Response(404, json={"error": "symbol not found"})
        )
        with pytest.raises(MDCResponseError) as exc_info:
            async with AsyncMDCClient(BASE_URL) as client:
                await client.snap("UNKN")
        assert exc_info.value.status_code == 404


class TestAsyncMDCClientHistory:
    @pytest.mark.asyncio
    @respx.mock
    async def test_history_returns_dataframe(self) -> None:
        respx.get(f"{BASE_URL}/api/historical").mock(
            return_value=Response(200, json=HISTORICAL_PAYLOAD)
        )
        async with AsyncMDCClient(BASE_URL) as client:
            df = await client.history("AAPL", from_date="2024-01-01", to_date="2024-01-31")
        assert isinstance(df, pd.DataFrame)
        assert len(df) == 2


class TestAsyncMDCClientStatus:
    @pytest.mark.asyncio
    @respx.mock
    async def test_status_returns_status_info(self) -> None:
        respx.get(f"{BASE_URL}/api/status").mock(return_value=Response(200, json=STATUS_PAYLOAD))
        async with AsyncMDCClient(BASE_URL) as client:
            info = await client.status()
        assert isinstance(info, StatusInfo)
        assert info.is_connected is True


# ===========================================================================
# Asynchronous client — live()
# ===========================================================================


class TestAsyncMDCClientLive:
    @pytest.mark.asyncio
    @respx.mock
    async def test_live_yields_dataframe_on_first_poll(self) -> None:
        respx.get(f"{BASE_URL}/api/data/trades/AAPL").mock(
            return_value=Response(200, json=TRADES_PAYLOAD)
        )
        batches: list[pd.DataFrame] = []
        async with AsyncMDCClient(BASE_URL) as client:
            async for batch in client.live("AAPL", interval=0):
                batches.append(batch)
                break
        assert len(batches) == 1
        assert isinstance(batches[0], pd.DataFrame)
        assert len(batches[0]) == 3

    @pytest.mark.asyncio
    @respx.mock
    async def test_live_deduplicates_by_sequence_number(self) -> None:
        """Second poll returns seq 1003 (already seen) + seq 1004 (new);
        only one new row should be emitted."""
        call_count = 0

        def side_effect(request):  # noqa: ANN001
            nonlocal call_count
            call_count += 1
            return Response(200, json=TRADES_PAYLOAD if call_count == 1 else TRADES_PAYLOAD_UPDATED)

        respx.get(f"{BASE_URL}/api/data/trades/AAPL").mock(side_effect=side_effect)

        batches: list[pd.DataFrame] = []
        async with AsyncMDCClient(BASE_URL) as client:
            async for batch in client.live("AAPL", interval=0):
                batches.append(batch)
                if len(batches) == 2:
                    break

        assert len(batches[0]) == 3  # seq 1001, 1002, 1003
        assert len(batches[1]) == 1  # only seq 1004
        assert int(batches[1]["sequence_number"].iloc[0]) == 1004

    @pytest.mark.asyncio
    @respx.mock
    async def test_live_skips_empty_polls(self) -> None:
        call_count = 0

        def side_effect(request):  # noqa: ANN001
            nonlocal call_count
            call_count += 1
            return Response(200, json=EMPTY_TRADES_PAYLOAD if call_count == 1 else TRADES_PAYLOAD)

        respx.get(f"{BASE_URL}/api/data/trades/AAPL").mock(side_effect=side_effect)

        batches: list[pd.DataFrame] = []
        async with AsyncMDCClient(BASE_URL) as client:
            async for batch in client.live("AAPL", interval=0):
                batches.append(batch)
                break

        assert call_count >= 2
        assert len(batches) == 1
        assert not batches[0].empty

    @pytest.mark.asyncio
    @respx.mock
    async def test_live_normalises_symbol_to_uppercase(self) -> None:
        route = respx.get(f"{BASE_URL}/api/data/trades/AAPL").mock(
            return_value=Response(200, json=TRADES_PAYLOAD)
        )
        async with AsyncMDCClient(BASE_URL) as client:
            async for _ in client.live("aapl", interval=0):
                break
        assert route.called

    @pytest.mark.asyncio
    @respx.mock
    async def test_live_raises_on_server_error(self) -> None:
        respx.get(f"{BASE_URL}/api/data/trades/AAPL").mock(
            return_value=Response(500, text="error")
        )
        with pytest.raises(MDCResponseError):
            async with AsyncMDCClient(BASE_URL) as client:
                async for _ in client.live("AAPL", interval=0):
                    pass


# ===========================================================================
# Model tests
# ===========================================================================


class TestModels:
    def test_trade_from_dict(self) -> None:
        from mdc.models import Trade
        raw = TRADES_PAYLOAD["trades"][0]
        t = Trade.from_dict(raw)
        assert t.symbol == "AAPL"
        assert t.price == 180.10
        assert t.size == 100
        assert t.aggressor == "BUY"
        assert t.sequence_number == 1001
        assert t.venue == "NYSE"

    def test_quote_from_dict(self) -> None:
        q = Quote.from_dict(QUOTES_PAYLOAD["quote"])
        assert q.bid_price == 180.10
        assert q.ask_price == 180.15
        assert q.mid_price == 180.125

    def test_orderbook_from_dict(self) -> None:
        ob = OrderBook.from_dict(ORDERBOOK_PAYLOAD)
        assert len(ob.bids) == 2
        assert ob.bids[0].price == 180.10
        assert ob.asks[0].price == 180.15

    def test_status_info_from_dict(self) -> None:
        info = StatusInfo.from_dict(STATUS_PAYLOAD)
        assert info.is_connected is True
        assert info.source_provider == "Alpaca"
        assert info.events_per_second == 42.5

    def test_status_info_uptime_zero_on_invalid_string(self) -> None:
        payload = {**STATUS_PAYLOAD, "uptime": "not-a-time"}
        info = StatusInfo.from_dict(payload)
        assert info.uptime_seconds == 0.0


# ===========================================================================
# Exception tests
# ===========================================================================


class TestExceptions:
    def test_mdc_response_error_carries_status_code(self) -> None:
        err = MDCResponseError(404, "not found")
        assert err.status_code == 404
        assert "404" in str(err)

    def test_mdc_connection_error_is_mdc_error(self) -> None:
        from mdc._exceptions import MDCError
        assert issubclass(MDCConnectionError, MDCError)

    def test_mdc_timeout_error_is_mdc_error(self) -> None:
        from mdc._exceptions import MDCError
        assert issubclass(MDCTimeoutError, MDCError)
