using System.IO;

namespace McpSqliteExplorer.Tests;

public sealed class McpSqliteExplorerOptionsValidatorTests
{
    private readonly McpSqliteExplorerOptionsValidator _validator = new();

    [Fact]
    public void Validate_WithDefaultsAndExistingDatabase_Succeeds()
    {
        using var db = new TestDatabase();

        var result = _validator.Validate(null, new McpSqliteExplorerOptions { DatabasePath = db.Path });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    public void Validate_WithPositiveMaxRows_Succeeds(int maxRows)
    {
        using var db = new TestDatabase();

        var result = _validator.Validate(null, new McpSqliteExplorerOptions { DatabasePath = db.Path, MaxRows = maxRows });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(600_000)]
    public void Validate_WithQueryTimeoutAtBoundaries_Succeeds(int timeoutMilliseconds)
    {
        using var db = new TestDatabase();

        var result = _validator.Validate(null, new McpSqliteExplorerOptions
        {
            DatabasePath = db.Path,
            QueryTimeout = TimeSpan.FromMilliseconds(timeoutMilliseconds),
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithZeroCatalogCacheTtl_Succeeds()
    {
        using var db = new TestDatabase();

        var result = _validator.Validate(null, new McpSqliteExplorerOptions
        {
            DatabasePath = db.Path,
            CatalogCacheTtl = TimeSpan.Zero,
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithMissingDatabaseAndAllowMissingDatabase_Succeeds()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.db");

        var result = _validator.Validate(null, new McpSqliteExplorerOptions
        {
            DatabasePath = missingPath,
            AllowMissingDatabase = true,
        });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithBlankDatabasePath_Fails(string databasePath)
    {
        var result = _validator.Validate(null, new McpSqliteExplorerOptions { DatabasePath = databasePath });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("DatabasePath must not be empty"));
    }

    [Fact]
    public void Validate_WithMissingDatabaseWithoutAllowFlag_Fails()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.db");

        var result = _validator.Validate(null, new McpSqliteExplorerOptions { DatabasePath = missingPath });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("does not exist as a file") && f.Contains("AllowMissingDatabase"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Validate_WithNonPositiveMaxRows_Fails(int maxRows)
    {
        using var db = new TestDatabase();

        var result = _validator.Validate(null, new McpSqliteExplorerOptions { DatabasePath = db.Path, MaxRows = maxRows });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("MaxRows must be greater than 0"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    [InlineData(600_001)]
    [InlineData(-1000)]
    public void Validate_WithQueryTimeoutOutsideRange_Fails(int timeoutMilliseconds)
    {
        using var db = new TestDatabase();

        var result = _validator.Validate(null, new McpSqliteExplorerOptions
        {
            DatabasePath = db.Path,
            QueryTimeout = TimeSpan.FromMilliseconds(timeoutMilliseconds),
        });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("QueryTimeout must be between"));
    }

    [Fact]
    public void Validate_WithNegativeCatalogCacheTtl_Fails()
    {
        using var db = new TestDatabase();

        var result = _validator.Validate(null, new McpSqliteExplorerOptions
        {
            DatabasePath = db.Path,
            CatalogCacheTtl = TimeSpan.FromSeconds(-1),
        });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("CatalogCacheTtl must not be negative"));
    }

    [Fact]
    public void Validate_WithEveryViolation_ReportsAllFailures()
    {
        var result = _validator.Validate(null, new McpSqliteExplorerOptions
        {
            DatabasePath = string.Empty,
            MaxRows = 0,
            QueryTimeout = TimeSpan.Zero,
            CatalogCacheTtl = TimeSpan.FromSeconds(-5),
        });

        Assert.False(result.Succeeded);
        Assert.Equal(4, result.Failures!.Count());
        Assert.Contains(result.Failures!, f => f.Contains("DatabasePath"));
        Assert.Contains(result.Failures!, f => f.Contains("MaxRows"));
        Assert.Contains(result.Failures!, f => f.Contains("QueryTimeout"));
        Assert.Contains(result.Failures!, f => f.Contains("CatalogCacheTtl"));
    }
}
