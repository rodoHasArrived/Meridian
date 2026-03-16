using System.Windows.Controls;
using MarketDataCollector.Wpf.ViewModels;

namespace MarketDataCollector.Wpf.Views;

/// <summary>
/// "Why MDC?" page — showcases MDC's competitive differentiators with a cost calculator,
/// feature comparison table, differentiator cards, and academic positioning.
/// Code-behind is thin: only DI wiring and DataContext assignment.
/// All state and display logic live in <see cref="DifferentiatorsViewModel"/>.
/// </summary>
public partial class DifferentiatorsPage : Page
{
    private readonly DifferentiatorsViewModel _viewModel;

    // DifferentiatorsViewModel has no service dependencies — it contains only
    // computed display data (cost calculations, static comparison rows, differentiator cards).
    // Direct instantiation is intentional; there is nothing to inject.
    public DifferentiatorsPage()
    {
        InitializeComponent();
        _viewModel = new DifferentiatorsViewModel();
        DataContext = _viewModel;
    }
}
