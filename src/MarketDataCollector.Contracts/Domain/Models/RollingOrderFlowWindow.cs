namespace MarketDataCollector.Contracts.Domain.Models;

/// <summary>
/// Rolling-window order-flow metrics.
/// </summary>
public sealed record RollingOrderFlowWindow(
    int WindowSeconds,
    long BuyVolume,
    long SellVolume,
    long UnknownVolume,
    decimal VWAP,
    decimal Imbalance
);
