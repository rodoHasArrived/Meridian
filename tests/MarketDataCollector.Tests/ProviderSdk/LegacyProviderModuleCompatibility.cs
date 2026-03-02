using MarketDataCollector.Infrastructure.DataSources;
using Microsoft.Extensions.DependencyInjection;

namespace MarketDataCollector.Infrastructure.Providers;

public interface IProviderModule
{
    void Register(IServiceCollection services, DataSourceRegistry registry);
}
