using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Ui.Services;

/// <summary>
/// No-op placeholder watchlist service for the shared UI services layer.
/// Returns empty data and does not persist state.
/// Platform-specific projects register their own implementations via the DI container.
/// </summary>
public sealed class WatchlistService
{
    private static readonly Lazy<WatchlistService> _instance = new(() => new WatchlistService());

    public static WatchlistService Instance => _instance.Value;

    public Task<WatchlistData> LoadWatchlistAsync()
        => Task.FromResult(new WatchlistData());

    /// <summary>
    /// No-op placeholder that always returns <see langword="false"/>.
    /// </summary>
    /// <param name="name">The watchlist name.</param>
    /// <param name="symbols">The symbols to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="false"/>.</returns>
    public Task<bool> CreateOrUpdateWatchlistAsync(string name, IEnumerable<string> symbols, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }
}

/// <summary>
/// Watchlist data containing watched symbols.
/// </summary>
public sealed class WatchlistData
{
    public List<WatchlistItem> Symbols { get; set; } = new();
    public List<WatchlistGroup> Groups { get; set; } = new();
}

/// <summary>
/// A single item in a watchlist.
/// </summary>
public sealed class WatchlistItem
{
    public string Symbol { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// A group of symbols in a watchlist.
/// </summary>
public sealed class WatchlistGroup
{
    public string Name { get; set; } = string.Empty;
    public List<string> Symbols { get; set; } = new();
}
