using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace McpSqliteExplorer;

/// <summary>
/// Startup configuration for the server. Bound from the <c>McpSqliteExplorer</c>
/// configuration section (environment variables, command line, or appsettings) and
/// validated by <see cref="McpSqliteExplorerOptionsValidator"/> when the host starts.
/// </summary>
public sealed class McpSqliteExplorerOptions
{
    /// <summary>Configuration section these options are bound from.</summary>
    public const string SectionName = "McpSqliteExplorer";

    /// <summary>Path to the SQLite file to expose. Must not be empty.</summary>
    public string DatabasePath { get; set; } = string.Empty;

    /// <summary>When true, a database file that does not exist yet passes validation.</summary>
    public bool AllowMissingDatabase { get; set; }

    /// <summary>Maximum number of rows a single query may return. Must be greater than zero.</summary>
    public int MaxRows { get; set; } = SqliteExplorer.MaxRowCap;

    /// <summary>Query time budget. Must be between one second and ten minutes, inclusive.</summary>
    public TimeSpan QueryTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Time-to-live for catalog cache entries. Must not be negative.</summary>
    public TimeSpan CatalogCacheTtl { get; set; } = TimeSpan.Zero;
}

/// <summary>
/// Registration helpers for <see cref="McpSqliteExplorerOptions"/>.
/// </summary>
public static class McpSqliteExplorerOptionsServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="McpSqliteExplorerOptions"/> from the <c>McpSqliteExplorer</c> section and
    /// registers its validator. Validation runs at host start, so an invalid configuration stops
    /// the server before any tool is served.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddMcpSqliteExplorerOptions(this IServiceCollection services)
    {
        services.AddOptions<McpSqliteExplorerOptions>()
            .BindConfiguration(McpSqliteExplorerOptions.SectionName)
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<McpSqliteExplorerOptions>, McpSqliteExplorerOptionsValidator>();

        return services;
    }
}
