using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpSqliteExplorer.Tests;

/// <summary>
/// Covers the startup log line that reports the effective configuration.
/// </summary>
public sealed class EffectiveOptionsLoggerTests
{
    private const string LogPrefix = "Effective configuration:";

    [Fact]
    public void Describe_ReportsEverySetting()
    {
        var options = new McpSqliteExplorerOptions
        {
            DatabasePath = "/data/app.db",
            AllowMissingDatabase = true,
            MaxRows = 250,
            QueryTimeout = TimeSpan.FromSeconds(30),
            CatalogCacheTtl = TimeSpan.FromMinutes(2),
            SchemaSummary = new SchemaSummaryOptions
            {
                IncludeTables = ["books", "loan.*"],
                ExcludeTables = ["tmp_.*"],
                CountRows = true,
                MaxCountedTables = 7,
                SampleRows = 3,
            },
        };

        var line = EffectiveOptionsLogger.Describe(options);

        Assert.Contains("DatabasePath='/data/app.db'", line);
        Assert.Contains("AllowMissingDatabase=True", line);
        Assert.Contains("MaxRows=250", line);
        Assert.Contains("QueryTimeout=00:00:30", line);
        Assert.Contains("CatalogCacheTtl=00:02:00", line);
        Assert.Contains("SchemaSummary.IncludeTables=['books', 'loan.*']", line);
        Assert.Contains("SchemaSummary.ExcludeTables=['tmp_.*']", line);
        Assert.Contains("SchemaSummary.CountRows=True", line);
        Assert.Contains("SchemaSummary.MaxCountedTables=7", line);
        Assert.Contains("SchemaSummary.SampleRows=3", line);
    }

    [Fact]
    public async Task StartAsync_WithValidConfiguration_LogsEffectiveConfigurationExactlyOnce()
    {
        using var db = new TestDatabase();
        var provider = new CapturingLoggerProvider();
        using var host = BuildHost(provider, new Dictionary<string, string?>
        {
            ["McpSqliteExplorer:DatabasePath"] = db.Path,
            ["McpSqliteExplorer:SchemaSummary:SampleRows"] = "2",
        });

        await host.StartAsync();
        await host.StopAsync();

        var lines = provider.Messages.Where(m => m.StartsWith(LogPrefix)).ToList();
        var line = Assert.Single(lines);
        Assert.Contains("SchemaSummary.SampleRows=2", line);
        Assert.Contains($"DatabasePath='{db.Path}'", line);
    }

    [Fact]
    public async Task StartAsync_WithInvalidConfiguration_DoesNotLogEffectiveConfiguration()
    {
        var provider = new CapturingLoggerProvider();
        using var host = BuildHost(provider, new Dictionary<string, string?>
        {
            ["McpSqliteExplorer:DatabasePath"] = string.Empty,
        });

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.DoesNotContain(provider.Messages, m => m.StartsWith(LogPrefix));
    }

    private static IHost BuildHost(ILoggerProvider provider, IDictionary<string, string?> settings)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(provider);
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddMcpSqliteExplorerOptions();

        return builder.Build();
    }

    /// <summary>Keeps every formatted log message so tests can count them.</summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                provider.Messages.Enqueue(formatter(state, exception));
        }
    }
}
