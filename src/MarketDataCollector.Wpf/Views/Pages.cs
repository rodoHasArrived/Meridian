using System.Windows.Controls;

namespace MarketDataCollector.Wpf.Views;

// Primary navigation pages
public sealed partial class DashboardPage : Page { }
public sealed partial class WatchlistPage : Page { }

// Data Sources pages
public sealed partial class ProviderPage : Page { }
public sealed partial class ProviderHealthPage : Page { }
public sealed partial class DataSourcesPage : Page { }

// Data Management pages
public sealed partial class LiveDataViewerPage : Page { }
public sealed partial class SymbolsPage : Page { }
public sealed partial class SymbolMappingPage : Page { }
public sealed partial class SymbolStoragePage : Page { }
public sealed partial class StoragePage : Page { }
public sealed partial class BackfillPage : Page { }
public sealed partial class PortfolioImportPage : Page { }
public sealed partial class IndexSubscriptionPage : Page { }
public sealed partial class OptionsPage : Page { }
public sealed partial class ScheduleManagerPage : Page { }

// Monitoring pages
public sealed partial class DataQualityPage : Page { }
public sealed partial class CollectionSessionPage : Page { }
public sealed partial class ArchiveHealthPage : Page { }
public sealed partial class ServiceManagerPage : Page { }
public sealed partial class SystemHealthPage : Page { }
public sealed partial class DiagnosticsPage : Page { }

// Tools pages
public sealed partial class DataExportPage : Page { }
public sealed partial class DataSamplingPage : Page { }
public sealed partial class TimeSeriesAlignmentPage : Page { }
public sealed partial class ExportPresetsPage : Page { }
public sealed partial class AnalysisExportPage : Page { }
public sealed partial class AnalysisExportWizardPage : Page { }
public sealed partial class EventReplayPage : Page { }
public sealed partial class PackageManagerPage : Page { }
public sealed partial class TradingHoursPage : Page { }

// Analytics & Visualization pages
public sealed partial class AdvancedAnalyticsPage : Page { }
public sealed partial class ChartingPage : Page { }
public sealed partial class OrderBookPage : Page { }
public sealed partial class DataCalendarPage : Page { }

// Storage & Maintenance pages
public sealed partial class StorageOptimizationPage : Page { }
public sealed partial class RetentionAssurancePage : Page { }
public sealed partial class AdminMaintenancePage : Page { }

// Integrations pages
public sealed partial class LeanIntegrationPage : Page { }
public sealed partial class MessagingHubPage : Page { }

// Backtesting pages
public sealed partial class BacktestPage : Page { }

// Workspaces & Notifications pages
public sealed partial class WorkspacePage : Page { }
public sealed partial class NotificationCenterPage : Page { }

// Support & Setup pages
public sealed partial class HelpPage : Page { }
public sealed partial class WelcomePage : Page { }
public sealed partial class SettingsPage : Page { }
public sealed partial class KeyboardShortcutsPage : Page { }
public sealed partial class SetupWizardPage : Page { }
public sealed partial class AddProviderWizardPage : Page { }

// Activity Log page
public sealed partial class ActivityLogPage : Page { }
