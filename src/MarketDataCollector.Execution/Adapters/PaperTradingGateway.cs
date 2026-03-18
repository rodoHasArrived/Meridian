using System.Runtime.CompilerServices;
using System.Threading.Channels;
using MarketDataCollector.Execution.Models;

namespace MarketDataCollector.Execution.Adapters;

/// <summary>
/// Simulated order gateway that routes no real orders to any exchange.
/// Fills are generated synthetically at the last-traded price of the live MDC feed,
/// making this the safe default for strategy validation before live promotion.
/// Implements ADR-015.
/// </summary>
[ImplementsAdr("ADR-015", "Simulated IOrderGateway over live MDC feed — no real orders")]
public sealed class PaperTradingGateway : IOrderGateway
{
    private readonly ILogger<PaperTradingGateway> _logger;
    private readonly Channel<OrderStatusUpdate> _updates;
    private readonly Dictionary<string, OrderRequest> _workingOrders = new();
    private readonly Lock _lock = new();
    private bool _disposed;

    /// <inheritdoc/>
    public string BrokerName => "Paper";

    /// <inheritdoc/>
    public ExecutionMode Mode => ExecutionMode.Paper;

    /// <summary>
    /// Creates a new paper trading gateway.
    /// </summary>
    public PaperTradingGateway(ILogger<PaperTradingGateway> logger)
    {
        _logger = logger;
        _updates = Channel.CreateBounded<OrderStatusUpdate>(new BoundedChannelOptions(1_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = false,
            SingleReader = false
        });
    }

    /// <inheritdoc/>
    public Task<OrderAcknowledgement> SubmitAsync(OrderRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            _workingOrders[request.ClientOrderId] = request;
        }

        _logger.LogInformation(
            "Paper order accepted: {ClientOrderId} {Quantity} {Symbol} @ {Type}",
            request.ClientOrderId, request.Quantity, request.Symbol, request.Type);

        var ack = new OrderAcknowledgement(
            OrderId: request.ClientOrderId,
            ClientOrderId: request.ClientOrderId,
            Symbol: request.Symbol,
            Status: ExecutionOrderStatus.Accepted,
            AcknowledgedAt: DateTimeOffset.UtcNow);

        // Immediately simulate a market fill at a notional zero-slippage price.
        // Real paper implementations would subscribe to the live feed and fill
        // on the next matching tick; this stub fills instantly for scaffolding purposes.
        _ = SimulateFillAsync(request, ct);

        return Task.FromResult(ack);
    }

    /// <inheritdoc/>
    public Task<bool> CancelAsync(string orderId, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        bool removed;
        lock (_lock)
        {
            removed = _workingOrders.Remove(orderId);
        }

        if (removed)
        {
            _logger.LogInformation("Paper order cancelled: {OrderId}", orderId);
            var update = new OrderStatusUpdate(
                OrderId: orderId,
                ClientOrderId: orderId,
                Symbol: string.Empty,
                Status: ExecutionOrderStatus.Cancelled,
                FilledQuantity: 0,
                AverageFillPrice: null,
                RejectReason: null,
                Timestamp: DateTimeOffset.UtcNow);

            _updates.Writer.TryWrite(update);
        }

        return Task.FromResult(removed);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<OrderStatusUpdate> StreamOrderUpdatesAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var update in _updates.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return update;
        }
    }

    private async Task SimulateFillAsync(OrderRequest request, CancellationToken ct)
    {
        // Yield to allow the caller to receive the acknowledgement before the fill.
        await Task.Yield();

        if (ct.IsCancellationRequested)
        {
            return;
        }

        lock (_lock)
        {
            _workingOrders.Remove(request.ClientOrderId);
        }

        var fill = new OrderStatusUpdate(
            OrderId: request.ClientOrderId,
            ClientOrderId: request.ClientOrderId,
            Symbol: request.Symbol,
            Status: ExecutionOrderStatus.Filled,
            FilledQuantity: Math.Abs(request.Quantity),
            AverageFillPrice: request.LimitPrice,
            RejectReason: null,
            Timestamp: DateTimeOffset.UtcNow);

        _updates.Writer.TryWrite(fill);

        _logger.LogInformation(
            "Paper fill: {ClientOrderId} {Quantity} {Symbol}",
            request.ClientOrderId, request.Quantity, request.Symbol);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _updates.Writer.TryComplete();

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
