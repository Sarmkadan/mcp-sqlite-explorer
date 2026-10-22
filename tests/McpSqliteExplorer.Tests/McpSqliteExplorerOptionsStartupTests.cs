using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace McpSqliteExplorer.Tests;

/// <summary>
/// Exercises the options through a real host so the ValidateOnStart wiring is covered,
/// not only the validator in isolation.
/// </summary>
public sealed class McpSqliteExplorerOptionsStartupTests
{
    private static IHost BuildHost(IDictionary<string, string?> settings)
    {
        var builder = Host.CreateApplicationBuilder();

        // Added last so these values win over any ambient environment variables.
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddMcpSqliteExplorerOptions();

        return builder.Build();
    }

    [Fact]
    public async Task StartAsync_WithValidConfiguration_Starts()
    {
        using var db = new TestDatabase();
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["McpSqliteExplorer:DatabasePath"] = db.Path,
        });

        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public async Task StartAsync_BindsTimeSpanAndIntegerSettingsFromConfiguration()
    {
        using var db = new TestDatabase();
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["McpSqliteExplorer:DatabasePath"] = db.Path,
            ["McpSqliteExplorer:MaxRows"] = "250",
            ["McpSqliteExplorer:QueryTimeout"] = "00:00:30",
            ["McpSqliteExplorer:CatalogCacheTtl"] = "00:02:00",
        });

        await host.StartAsync();

        var options = host.Services.GetRequiredService<IOptions<McpSqliteExplorerOptions>>().Value;
        Assert.Equal(250, options.MaxRows);
        Assert.Equal(TimeSpan.FromSeconds(30), options.QueryTimeout);
        Assert.Equal(TimeSpan.FromMinutes(2), options.CatalogCacheTtl);

        await host.StopAsync();
    }

    [Fact]
    public async Task StartAsync_WithInvalidConfiguration_ThrowsListingEveryViolation()
    {
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["McpSqliteExplorer:DatabasePath"] = string.Empty,
            ["McpSqliteExplorer:MaxRows"] = "0",
            ["McpSqliteExplorer:QueryTimeout"] = "00:00:00.500",
            ["McpSqliteExplorer:CatalogCacheTtl"] = "-00:00:01",
        });

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Equal(4, ex.Failures.Count());
        Assert.Contains(ex.Failures, f => f.Contains("DatabasePath"));
        Assert.Contains(ex.Failures, f => f.Contains("MaxRows"));
        Assert.Contains(ex.Failures, f => f.Contains("QueryTimeout"));
        Assert.Contains(ex.Failures, f => f.Contains("CatalogCacheTtl"));
    }

    [Fact]
    public async Task StartAsync_WithMissingDatabaseFile_ThrowsWithReadableFailure()
    {
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["McpSqliteExplorer:DatabasePath"] = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.db"),
        });

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Single(ex.Failures);
        Assert.Contains("does not exist as a file", ex.Failures.Single());
    }
}
