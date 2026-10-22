using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpSqliteExplorer;

/// <summary>
/// Writes the effective <see cref="McpSqliteExplorerOptions"/> to the log once, when the host starts.
/// Registered after the options validation, so the line only appears for a configuration that passed.
/// </summary>
internal sealed class EffectiveOptionsLogger : IHostedService
{
    private static readonly Action<ILogger, string, Exception?> EffectiveConfigurationLog =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(5, "EffectiveConfiguration"),
            "Effective configuration: {Settings}");

    private readonly IOptions<McpSqliteExplorerOptions> _options;
    private readonly ILogger<EffectiveOptionsLogger> _logger;

    /// <summary>
    /// Creates the logger.
    /// </summary>
    /// <param name="options">The validated options to report.</param>
    /// <param name="logger">Destination for the log line.</param>
    public EffectiveOptionsLogger(IOptions<McpSqliteExplorerOptions> options, ILogger<EffectiveOptionsLogger> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        EffectiveConfigurationLog(_logger, Describe(_options.Value), null);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Formats the options as one line of <c>name=value</c> pairs, in the order an operator would read them.
    /// </summary>
    /// <param name="options">The options to format.</param>
    /// <returns>The formatted settings.</returns>
    internal static string Describe(McpSqliteExplorerOptions options)
    {
        var summary = options.SchemaSummary;

        return string.Join(", ",
            $"DatabasePath='{options.DatabasePath}'",
            $"AllowMissingDatabase={options.AllowMissingDatabase}",
            $"MaxRows={options.MaxRows}",
            $"QueryTimeout={options.QueryTimeout}",
            $"CatalogCacheTtl={options.CatalogCacheTtl}",
            $"SchemaSummary.IncludeTables=[{Quote(summary.IncludeTables)}]",
            $"SchemaSummary.ExcludeTables=[{Quote(summary.ExcludeTables)}]",
            $"SchemaSummary.CountRows={summary.CountRows}",
            $"SchemaSummary.MaxCountedTables={summary.MaxCountedTables}",
            $"SchemaSummary.SampleRows={summary.SampleRows}");
    }

    private static string Quote(string[] patterns) =>
        string.Join(", ", patterns.Select(pattern => $"'{pattern}'"));
}
