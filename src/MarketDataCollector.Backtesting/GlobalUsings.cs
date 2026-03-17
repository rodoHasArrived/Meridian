global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Runtime.CompilerServices;
global using System.Threading;
global using System.Threading.Tasks;
global using Microsoft.Extensions.Logging;
global using MarketDataCollector.Backtesting.Sdk;
global using MarketDataCollector.Contracts.Domain.Models;

// Expose internal classes to test assembly for unit testing
[assembly: InternalsVisibleTo("MarketDataCollector.Backtesting.Tests")]
