using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace McpSqliteExplorer.Tests;

/// <summary>
/// Binds the <c>SchemaSummary</c> subsection from configuration and checks that its rules run through
/// the real host, so the ValidateOnStart wiring is covered as well as the validator itself.
/// </summary>
public sealed class SchemaSummaryOptionsStartupTests
{
    private static IHost BuildHost(IDictionary<string, string?> settings)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddMcpSqliteExplorerOptions();

        return builder.Build();
    }

    [Fact]
    public async Task StartAsync_BindsSchemaSummaryFromConfiguration()
    {
        using var db = new TestDatabase();
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["McpSqliteExplorer:DatabasePath"] = db.Path,
            ["McpSqliteExplorer:SchemaSummary:IncludeTables:0"] = "books",
            ["McpSqliteExplorer:SchemaSummary:IncludeTables:1"] = "loan.*",
            ["McpSqliteExplorer:SchemaSummary:ExcludeTables:0"] = "recent_.*",
            ["McpSqliteExplorer:SchemaSummary:CountRows"] = "true",
            ["McpSqliteExplorer:SchemaSummary:MaxCountedTables"] = "5",
            ["McpSqliteExplorer:SchemaSummary:SampleRows"] = "3",
        });

        await host.StartAsync();

        var summary = host.Services.GetRequiredService<IOptions<McpSqliteExplorerOptions>>().Value.SchemaSummary;
        Assert.Equal(["books", "loan.*"], summary.IncludeTables);
        Assert.Equal(["recent_.*"], summary.ExcludeTables);
        Assert.True(summary.CountRows);
        Assert.Equal(5, summary.MaxCountedTables);
        Assert.Equal(3, summary.SampleRows);

        await host.StopAsync();
    }

    [Fact]
    public async Task StartAsync_WithDefaultSchemaSummary_Starts()
    {
        using var db = new TestDatabase();
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["McpSqliteExplorer:DatabasePath"] = db.Path,
        });

        await host.StartAsync();

        var summary = host.Services.GetRequiredService<IOptions<McpSqliteExplorerOptions>>().Value.SchemaSummary;
        Assert.Empty(summary.IncludeTables);
        Assert.Empty(summary.ExcludeTables);
        Assert.False(summary.CountRows);
        Assert.Equal(100, summary.MaxCountedTables);
        Assert.Equal(0, summary.SampleRows);

        await host.StopAsync();
    }

    [Fact]
    public async Task StartAsync_WithConflictingIncludeAndExclude_ThrowsBeforeStarting()
    {
        using var db = new TempDatabaseWithTables(("books", 1));
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["McpSqliteExplorer:DatabasePath"] = db.Path,
            ["McpSqliteExplorer:SchemaSummary:IncludeTables:0"] = "books",
            ["McpSqliteExplorer:SchemaSummary:ExcludeTables:0"] = "books",
        });

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        var failure = Assert.Single(ex.Failures);
        Assert.Contains("matched by both IncludeTables and ExcludeTables", failure);
    }

    [Fact]
    public async Task StartAsync_WithOutOfRangeSampleRows_ThrowsListingTheRule()
    {
        using var db = new TestDatabase();
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["McpSqliteExplorer:DatabasePath"] = db.Path,
            ["McpSqliteExplorer:SchemaSummary:SampleRows"] = "1001",
        });

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        var failure = Assert.Single(ex.Failures);
        Assert.Contains("SchemaSummary:SampleRows must be between 0 and 1000", failure);
    }
}
