using System.Collections.ObjectModel;

namespace MarketDataCollector.Wpf.ViewModels;

/// <summary>
/// Represents a single competitive differentiator card shown on the Why MDC page.
/// </summary>
public sealed record DifferentiatorCard(
    string Icon,
    string IconColorHex,
    string Title,
    string Description,
    string Badge,
    string BadgeColorHex);

/// <summary>
/// Represents a single feature row in the competitor comparison table.
/// </summary>
public sealed record ComparisonRow(
    string Feature,
    string MdcValue,
    string PolygonValue,
    string DatabentoValue,
    string BloombergValue);

/// <summary>
/// ViewModel for the "Why MDC?" Differentiators page.
/// Surfaces cost calculator, feature comparison, and differentiator cards
/// based on the brainstormed competitive advantages of Market Data Collector.
/// </summary>
public sealed class DifferentiatorsViewModel : BindableBase
{
    // Polygon Developer plan — covers unlimited symbols, flat fee.
    // Verified: polygon.io pricing, Developer tier, March 2026.
    private const decimal PolygonFlatMonthly = 79m;

    // Databento approximate per-symbol per-month cost (real-time tier, ~$0.10–$1.00/symbol/day).
    // Using $10/symbol/month as a conservative mid-range estimate. Verified: databento.com, March 2026.
    private const decimal DatabentoPerSymbolMonthly = 10m;

    // Bloomberg Terminal approximate cost per user per month (~$24,000/year/seat).
    // Verified: Bloomberg Terminal pricing guide, March 2026.
    private const decimal BloombergMonthly = 2000m;

    private int _symbolCount = 100;

    public DifferentiatorsViewModel()
    {
        ComparisonRows = BuildComparisonRows();
        DifferentiatorCards = BuildDifferentiatorCards();
    }

    /// <summary>Number of symbols in the cost calculator (slider input).</summary>
    public int SymbolCount
    {
        get => _symbolCount;
        set
        {
            if (SetProperty(ref _symbolCount, Math.Clamp(value, 10, 5000)))
            {
                RaisePropertyChanged(nameof(MdcMonthlyCostText));
                RaisePropertyChanged(nameof(PolygonMonthlyCostText));
                RaisePropertyChanged(nameof(DatabentoMonthlyCostText));
                RaisePropertyChanged(nameof(BloombergMonthlyCostText));
                RaisePropertyChanged(nameof(AnnualSavingsVsPolygonText));
                RaisePropertyChanged(nameof(AnnualSavingsVsDatabentoText));
                RaisePropertyChanged(nameof(SavingsLabelText));
            }
        }
    }

    // ── Cost display properties ──────────────────────────────────────────────

    public string MdcMonthlyCostText => "$0 / month";

    public string PolygonMonthlyCostText => $"${PolygonFlatMonthly:N0} / month";

    public string DatabentoMonthlyCostText
    {
        get
        {
            var cost = DatabentoPerSymbolMonthly * SymbolCount;
            return cost >= 10000m
                ? $"${cost / 1000m:N0}K / month"
                : $"${cost:N0} / month";
        }
    }

    public string BloombergMonthlyCostText => $"${BloombergMonthly:N0} / month";

    public string AnnualSavingsVsPolygonText
    {
        get
        {
            var savings = PolygonFlatMonthly * 12;
            return $"${savings:N0} saved vs Polygon";
        }
    }

    public string AnnualSavingsVsDatabentoText
    {
        get
        {
            var savings = DatabentoPerSymbolMonthly * SymbolCount * 12;
            return savings >= 10000m
                ? $"${savings / 1000m:N1}K saved vs Databento"
                : $"${savings:N0} saved vs Databento";
        }
    }

    public string SavingsLabelText
    {
        get
        {
            var cheapestPaid = Math.Min(PolygonFlatMonthly, DatabentoPerSymbolMonthly * SymbolCount) * 12;
            return cheapestPaid >= 10000m
                ? $"${cheapestPaid / 1000m:N1}K / year saved vs cheapest paid alternative"
                : $"${cheapestPaid:N0} / year saved vs cheapest paid alternative";
        }
    }

    // ── Comparison table and cards ───────────────────────────────────────────

    public ObservableCollection<ComparisonRow> ComparisonRows { get; }
    public ObservableCollection<DifferentiatorCard> DifferentiatorCards { get; }

    // ── Builders ─────────────────────────────────────────────────────────────

    private static ObservableCollection<ComparisonRow> BuildComparisonRows() => new(
    [
        new("Self-hosted (on-premise)",     "✓ Always",      "✗ Cloud only",  "✗ Cloud only",   "✗ Cloud only"),
        new("Zero monthly licensing cost",  "✓ $0 / month",  "✗ $79+/month",  "✗ $150+/month",  "✗ $2,000/month"),
        new("Multi-provider failover",      "✓ 90+ sources", "✗ Single",      "✗ Single",       "✓ Internal"),
        new("Air-gapped / offline mode",    "✓ --offline",   "✗ Requires net","✗ Requires net", "✗ Requires net"),
        new("Crash-safe WAL storage",       "✓ Built-in",    "✗ None",        "✗ None",         "✗ Opaque"),
        new("Hot config reload",            "✓ 350ms debounce","✗ Restart req","✗ Restart req", "✗ Restart req"),
        new("Open plugin / provider SDK",   "✓ [DataSource]","✗ Closed",      "✗ Closed",       "✗ Closed"),
        new("LEAN backtesting integration", "✓ Native",      "✗ Manual",      "✗ Manual",       "✗ Manual"),
        new("F# type-safe domain model",    "✓ DU validation","✗ Stringly typed","✗ Stringly typed","✗ Opaque"),
        new("Free tier",                    "✓ Always free", "✓ Limited",     "✗ Paid only",    "✗ Paid only"),
    ]);

    private static ObservableCollection<DifferentiatorCard> BuildDifferentiatorCards() => new(
    [
        new(
            Icon: "\uE8C7",
            IconColorHex: "#3fb950",
            Title: "Zero Cost at Any Scale",
            Description: "Collect from IB TWS + Alpaca at $0/month. The more symbols you collect, the bigger your savings vs. paid alternatives — MDC is the only tool where scale works in your favor.",
            Badge: "Saves $948+/yr vs Polygon",
            BadgeColorHex: "#3fb950"),

        new(
            Icon: "\uE72E",
            IconColorHex: "#58a6ff",
            Title: "Self-Hosted & Air-Gapped",
            Description: "Run entirely on-premise with --dry-run --offline validation. No data leaves your network. Suitable for MiFID II residency requirements and SEC-regulated environments.",
            Badge: "Compliance-Ready",
            BadgeColorHex: "#58a6ff"),

        new(
            Icon: "\uE968",
            IconColorHex: "#a371f7",
            Title: "Multi-Provider Failover",
            Description: "90+ data sources via StockSharp. When your primary provider disconnects, MDC switches automatically, records the gap, and backfills on reconnect — no manual intervention.",
            Badge: "90+ Sources",
            BadgeColorHex: "#a371f7"),

        new(
            Icon: "\uE943",
            IconColorHex: "#39c5cf",
            Title: "F# Type-Safe Domain",
            Description: "Market events use F# discriminated unions. A ValidationResult<TradeEvent> is Result<T, ValidationError list> — bad ticks are structurally impossible to ignore downstream.",
            Badge: "Compile-Time Safety",
            BadgeColorHex: "#39c5cf"),

        new(
            Icon: "\uEE94",
            IconColorHex: "#d29922",
            Title: "Crash-Safe WAL Storage",
            Description: "Every tick is written to a checksum-verified Write-Ahead Log before commit. A power failure during expiration doesn't lose data. Recovery happens automatically on startup.",
            Badge: "Exactly-Once Guarantee",
            BadgeColorHex: "#d29922"),

        new(
            Icon: "\uE72C",
            IconColorHex: "#56d364",
            Title: "Hot-Config Reload",
            Description: "Add TSLA to your symbol universe mid-session without stopping the collector. ConfigWatcher debounces file changes at 350ms — no pipeline interruption, no missed ticks.",
            Badge: "Zero-Downtime Updates",
            BadgeColorHex: "#56d364"),

        new(
            Icon: "\uE9D2",
            IconColorHex: "#f78166",
            Title: "Collect-Once, Backtest-Anywhere",
            Description: "MDC's LEAN integration exports tick data in QuantConnect's native ZIP/CSV format. Collect live data with MDC, backtest strategies with LEAN — no manual data wrangling.",
            Badge: "LEAN Native",
            BadgeColorHex: "#f78166"),

        new(
            Icon: "\uEA86",
            IconColorHex: "#db61a2",
            Title: "Open Plugin SDK",
            Description: "Implement IMarketDataClient, add [DataSource(\"my-exchange\")], drop the DLL in the plugins folder — it's auto-discovered. The architecture is already MEF-style; community providers just work.",
            Badge: "dotnet new mdc-provider",
            BadgeColorHex: "#db61a2"),
    ]);
}
