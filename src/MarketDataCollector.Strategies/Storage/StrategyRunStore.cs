using MarketDataCollector.Strategies.Models;

namespace MarketDataCollector.Strategies.Storage;

/// <summary>
/// In-memory run store used for development and testing.
/// A production implementation would persist entries to JSONL using
/// <c>AtomicFileWriter</c> following the same pattern as <c>JsonlStorageSink</c>.
/// </summary>
public sealed class StrategyRunStore : IStrategyRepository
{
    private readonly List<StrategyRunEntry> _runs = new();
    private readonly Lock _lock = new();

    /// <inheritdoc/>
    public Task RecordRunAsync(StrategyRunEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_lock)
        {
            // Update existing entry by RunId, or append a new one.
            var idx = _runs.FindIndex(r => r.RunId == entry.RunId);
            if (idx >= 0)
            {
                _runs[idx] = entry;
            }
            else
            {
                _runs.Add(entry);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<StrategyRunEntry> GetRunsAsync(
        string strategyId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        List<StrategyRunEntry> snapshot;
        lock (_lock)
        {
            snapshot = _runs
                .Where(r => r.StrategyId == strategyId)
                .OrderBy(r => r.StartedAt)
                .ToList();
        }

        foreach (var entry in snapshot)
        {
            ct.ThrowIfCancellationRequested();
            yield return entry;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<StrategyRunEntry?> GetLatestRunAsync(string strategyId, CancellationToken ct = default)
    {
        StrategyRunEntry? latest;
        lock (_lock)
        {
            latest = _runs
                .Where(r => r.StrategyId == strategyId)
                .MaxBy(r => r.StartedAt);
        }

        return Task.FromResult(latest);
    }
}
