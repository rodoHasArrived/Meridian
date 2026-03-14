using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarketDataCollector.Wpf.ViewModels;
using WpfServices = MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf.Views;

public partial class DataBrowserPage : Page
{
    private const string PageTag = "DataBrowser";

    private readonly DataBrowserViewModel _viewModel = new();

    public DataBrowserPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Unloaded += OnPageUnloaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        RestoreFilterState();
        _viewModel.RefreshResults();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void ApplyFilters_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RefreshResults();
    }

    private void ResetFilters_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetFilters();
    }

    private void PreviousPage_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.GoToPreviousPage();
    }

    private void NextPage_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.GoToNextPage();
    }

    private void SortChanged_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortCombo.SelectedItem is ComboBoxItem item && item.Tag is string sortKey)
        {
            WpfServices.PageStateService.Instance.SetFilter(PageTag, "sort", sortKey);
            _viewModel.SortField = sortKey;
            _viewModel.RefreshResults();
        }
    }

    private void RestoreFilterState()
    {
        var pss = WpfServices.PageStateService.Instance;
        var sort = pss.GetFilter(PageTag, "sort");
        if (sort != null) SelectComboItemByTag(SortCombo, sort);
        var symbolFilter = pss.GetFilter(PageTag, "symbolFilter");
        if (symbolFilter != null) _viewModel.SymbolFilter = symbolFilter;
        var dataType = pss.GetFilter(PageTag, "dataType");
        if (dataType != null) _viewModel.SelectedDataType = dataType;
        var venue = pss.GetFilter(PageTag, "venue");
        if (venue != null) _viewModel.SelectedVenue = venue;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var pss = WpfServices.PageStateService.Instance;
        switch (e.PropertyName)
        {
            case nameof(DataBrowserViewModel.SymbolFilter):
                pss.SetFilter(PageTag, "symbolFilter",
                    string.IsNullOrWhiteSpace(_viewModel.SymbolFilter) ? null : _viewModel.SymbolFilter);
                break;
            case nameof(DataBrowserViewModel.SelectedDataType):
                pss.SetFilter(PageTag, "dataType",
                    _viewModel.SelectedDataType == "All" ? null : _viewModel.SelectedDataType);
                break;
            case nameof(DataBrowserViewModel.SelectedVenue):
                pss.SetFilter(PageTag, "venue",
                    _viewModel.SelectedVenue == "All" ? null : _viewModel.SelectedVenue);
                break;
        }
    }

    private static void SelectComboItemByTag(ComboBox combo, string tag)
    {
        foreach (var item in combo.Items)
            if (item is ComboBoxItem cbi && cbi.Tag?.ToString() == tag)
            { combo.SelectedItem = item; return; }
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ExportToCsv();
    }

    private void ResultsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultsGrid.SelectedItem is DataBrowserRecord record)
        {
            DetailPanel.Visibility = Visibility.Visible;
            DetailSymbol.Text = record.Symbol;
            DetailVenue.Text = record.Venue;
            DetailDataType.Text = record.DataType;
            DetailTimestamp.Text = record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            DetailPrice.Text = record.Price.ToString("N2");
            DetailSize.Text = record.Size.ToString("N0");
        }
        else
        {
            DetailPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void CopyJson_Click(object sender, RoutedEventArgs e)
    {
        if (ResultsGrid.SelectedItem is not DataBrowserRecord record) return;

        var json = JsonSerializer.Serialize(new
        {
            record.Symbol,
            record.DataType,
            record.Venue,
            Timestamp = record.Timestamp.ToString("O"),
            record.Price,
            record.Size
        }, new JsonSerializerOptions { WriteIndented = true });

        Clipboard.SetText(json);
    }
}

public sealed class DataBrowserViewModel : BindableBase, IDataErrorInfo
{
    private readonly List<DataBrowserRecord> _allRecords;
    private readonly ObservableCollection<DataBrowserRecord> _pagedRecords = new();
    private string _symbolFilter = string.Empty;
    private string _selectedDataType = "All";
    private string _selectedVenue = "All";
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private int _pageSize = 25;
    private int _currentPage = 1;
    private string _validationSummary = string.Empty;
    private string _sortField = "TimestampDesc";
    private string _filteredCountText = "0 records";

    public DataBrowserViewModel()
    {
        _allRecords = BuildSampleData();
        DataTypes = new ObservableCollection<string> { "All", "Trades", "Quotes", "Depth" };
        Venues = new ObservableCollection<string> { "All", "NYSE", "NASDAQ", "ARCA", "SMART" };
        PageSizes = new ObservableCollection<int> { 25, 50, 100, 250 };
    }

    public ObservableCollection<string> DataTypes { get; }

    public ObservableCollection<string> Venues { get; }

    public ObservableCollection<int> PageSizes { get; }

    public ObservableCollection<DataBrowserRecord> PagedRecords => _pagedRecords;

    public string SortField
    {
        get => _sortField;
        set => SetProperty(ref _sortField, value);
    }

    public string FilteredCountText
    {
        get => _filteredCountText;
        private set => SetProperty(ref _filteredCountText, value);
    }

    public string SymbolFilter
    {
        get => _symbolFilter;
        set => SetProperty(ref _symbolFilter, value);
    }

    public string SelectedDataType
    {
        get => _selectedDataType;
        set => SetProperty(ref _selectedDataType, value);
    }

    public string SelectedVenue
    {
        get => _selectedVenue;
        set => SetProperty(ref _selectedVenue, value);
    }

    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
            {
                UpdateValidationSummary();
            }
        }
    }

    public DateTime? ToDate
    {
        get => _toDate;
        set
        {
            if (SetProperty(ref _toDate, value))
            {
                UpdateValidationSummary();
            }
        }
    }

    public int PageSize
    {
        get => _pageSize;
        set
        {
            if (SetProperty(ref _pageSize, value))
            {
                _currentPage = 1;
                RefreshResults();
            }
        }
    }

    public string PageSummary => $"Page {_currentPage} of {TotalPages} · {FilteredCount} records";

    public bool CanGoPrevious => _currentPage > 1;

    public bool CanGoNext => _currentPage < TotalPages;

    public string ValidationSummary
    {
        get => _validationSummary;
        private set => SetProperty(ref _validationSummary, value);
    }

    private int FilteredCount { get; set; }

    private int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(FilteredCount / (double)PageSize));

    public string Error => string.Empty;

    public string this[string columnName]
    {
        get
        {
            if ((columnName == nameof(FromDate) || columnName == nameof(ToDate)) &&
                FromDate.HasValue && ToDate.HasValue && FromDate > ToDate)
            {
                return "Start date must be before the end date.";
            }

            return string.Empty;
        }
    }

    public void RefreshResults()
    {
        var query = _allRecords.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SymbolFilter))
        {
            query = query.Where(record => record.Symbol.Contains(SymbolFilter.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedDataType, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(record => record.DataType.Equals(SelectedDataType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedVenue, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(record => record.Venue.Equals(SelectedVenue, StringComparison.OrdinalIgnoreCase));
        }

        if (FromDate.HasValue)
        {
            query = query.Where(record => record.Timestamp >= FromDate.Value);
        }

        if (ToDate.HasValue)
        {
            query = query.Where(record => record.Timestamp <= ToDate.Value);
        }

        var sorted = SortField switch
        {
            "TimestampAsc" => query.OrderBy(r => r.Timestamp),
            "SymbolAsc" => query.OrderBy(r => r.Symbol).ThenByDescending(r => r.Timestamp),
            "SymbolDesc" => query.OrderByDescending(r => r.Symbol).ThenByDescending(r => r.Timestamp),
            "PriceDesc" => query.OrderByDescending(r => r.Price),
            "PriceAsc" => query.OrderBy(r => r.Price),
            "SizeDesc" => query.OrderByDescending(r => r.Size),
            _ => query.OrderByDescending(r => r.Timestamp), // TimestampDesc default
        };

        var filtered = sorted.ToList();
        FilteredCount = filtered.Count;
        FilteredCountText = $"{FilteredCount:N0} records";

        var paged = filtered.Skip((_currentPage - 1) * PageSize).Take(PageSize).ToList();
        _pagedRecords.Clear();
        foreach (var record in paged)
        {
            _pagedRecords.Add(record);
        }

        RaisePropertyChanged(nameof(PageSummary));
        RaisePropertyChanged(nameof(CanGoPrevious));
        RaisePropertyChanged(nameof(CanGoNext));
        UpdateValidationSummary();
    }

    public void GoToPreviousPage()
    {
        if (!CanGoPrevious)
        {
            return;
        }

        _currentPage--;
        RefreshResults();
    }

    public void GoToNextPage()
    {
        if (!CanGoNext)
        {
            return;
        }

        _currentPage++;
        RefreshResults();
    }

    public void ResetFilters()
    {
        SymbolFilter = string.Empty;
        SelectedDataType = "All";
        SelectedVenue = "All";
        FromDate = null;
        ToDate = null;
        _currentPage = 1;
        RefreshResults();
    }

    public void ExportToCsv()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"data-browser-export-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };

        if (dialog.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,Symbol,DataType,Venue,Price,Size");
        foreach (var record in _pagedRecords)
        {
            sb.AppendLine($"{record.Timestamp:O},{record.Symbol},{record.DataType},{record.Venue},{record.Price},{record.Size}");
        }

        File.WriteAllText(dialog.FileName, sb.ToString());
    }

    private void UpdateValidationSummary()
    {
        ValidationSummary = this[nameof(FromDate)];
    }

    private static List<DataBrowserRecord> BuildSampleData()
    {
        var random = new Random(42);
        var symbols = new[] { "AAPL", "MSFT", "NVDA", "SPY", "QQQ", "TSLA", "AMZN" };
        var dataTypes = new[] { "Trades", "Quotes", "Depth" };
        var venues = new[] { "NYSE", "NASDAQ", "ARCA", "SMART" };
        var records = new List<DataBrowserRecord>();

        for (var i = 0; i < 240; i++)
        {
            var symbol = symbols[random.Next(symbols.Length)];
            var dataType = dataTypes[random.Next(dataTypes.Length)];
            var venue = venues[random.Next(venues.Length)];
            var timestamp = DateTime.Today.AddMinutes(-random.Next(0, 7200));
            records.Add(new DataBrowserRecord
            {
                Symbol = symbol,
                DataType = dataType,
                Venue = venue,
                Timestamp = timestamp,
                Price = Math.Round(50 + random.NextDouble() * 250, 2),
                Size = random.Next(10, 1000)
            });
        }

        return records;
    }
}

public sealed class DataBrowserRecord
{
    private static readonly SolidColorBrush TradeBrush = new(Color.FromRgb(0x4E, 0xC9, 0xB0)); // teal
    private static readonly SolidColorBrush QuoteBrush = new(Color.FromRgb(0x56, 0x9C, 0xD6)); // blue
    private static readonly SolidColorBrush DepthBrush = new(Color.FromRgb(0xDC, 0xDC, 0xAA)); // gold

    public DateTime Timestamp { get; init; }

    public string Symbol { get; init; } = string.Empty;

    public string DataType { get; init; } = string.Empty;

    public string Venue { get; init; } = string.Empty;

    public double Price { get; init; }

    public int Size { get; init; }

    public SolidColorBrush DataTypeColor => DataType switch
    {
        "Trades" => TradeBrush,
        "Quotes" => QuoteBrush,
        "Depth" => DepthBrush,
        _ => QuoteBrush
    };
}
