using System.Linq;
using System.Text.Json;

namespace McpSqliteExplorer.Tests;

/// <summary>
/// Covers how <see cref="SqliteToolsExtensions.GetSchemaSummary(SqliteExplorer, SchemaSummaryOptions)"/>
/// applies selection, row counting and sampling.
/// </summary>
public sealed class SqliteToolsExtensionsSchemaSummaryTests
{
    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void GetSchemaSummary_WithDefaultOptions_OmitsRowCountsAndSamples()
    {
        using var db = new TempDatabaseWithTables(("authors", 2), ("books", 3), ("tmp_old", 1));
        using var explorer = new SqliteExplorer(db.Path);

        var root = Parse(explorer.GetSchemaSummary(new SchemaSummaryOptions()));

        Assert.Equal(3, root.GetProperty("tables").GetInt32());
        Assert.Equal(3, root.GetProperty("totalObjects").GetInt32());
        Assert.False(root.TryGetProperty("rowCounts", out _));
        Assert.False(root.TryGetProperty("rowCountsSkipped", out _));
        Assert.False(root.TryGetProperty("samples", out _));
    }

    [Fact]
    public void GetSchemaSummary_WithIncludePattern_ReportsOnlyMatchingTables()
    {
        using var db = new TempDatabaseWithTables(("authors", 2), ("books", 3), ("tmp_old", 1));
        using var explorer = new SqliteExplorer(db.Path);

        var root = Parse(explorer.GetSchemaSummary(new SchemaSummaryOptions { IncludeTables = ["^books$"] }));

        Assert.Equal(1, root.GetProperty("totalObjects").GetInt32());
        Assert.Equal(1, root.GetProperty("tables").GetInt32());
    }

    [Fact]
    public void GetSchemaSummary_WithExcludePattern_OmitsMatchingTables()
    {
        using var db = new TempDatabaseWithTables(("authors", 2), ("books", 3), ("tmp_old", 1));
        using var explorer = new SqliteExplorer(db.Path);

        var root = Parse(explorer.GetSchemaSummary(new SchemaSummaryOptions { ExcludeTables = ["tmp_.*"] }));

        Assert.Equal(2, root.GetProperty("totalObjects").GetInt32());
    }

    [Fact]
    public void GetSchemaSummary_PatternsAreCaseInsensitive()
    {
        using var db = new TempDatabaseWithTables(("Authors", 2), ("books", 3));
        using var explorer = new SqliteExplorer(db.Path);

        var root = Parse(explorer.GetSchemaSummary(new SchemaSummaryOptions { IncludeTables = ["AUTHORS"] }));

        Assert.Equal(1, root.GetProperty("totalObjects").GetInt32());
    }

    [Fact]
    public void GetSchemaSummary_PatternMustMatchWholeName()
    {
        using var db = new TempDatabaseWithTables(("books", 3), ("books_archive", 1));
        using var explorer = new SqliteExplorer(db.Path);

        var root = Parse(explorer.GetSchemaSummary(new SchemaSummaryOptions { IncludeTables = ["books"] }));

        Assert.Equal(1, root.GetProperty("totalObjects").GetInt32());
    }

    [Fact]
    public void GetSchemaSummary_WithCountRows_ReportsCountPerSelectedTable()
    {
        using var db = new TempDatabaseWithTables(("authors", 2), ("books", 3), ("tmp_old", 1));
        using var explorer = new SqliteExplorer(db.Path);

        var root = Parse(explorer.GetSchemaSummary(new SchemaSummaryOptions
        {
            CountRows = true,
            ExcludeTables = ["tmp_.*"],
        }));

        Assert.False(root.GetProperty("rowCountsSkipped").GetBoolean());
        var counts = root.GetProperty("rowCounts");
        Assert.Equal(2, counts.GetProperty("authors").GetInt64());
        Assert.Equal(3, counts.GetProperty("books").GetInt64());
        Assert.False(counts.TryGetProperty("tmp_old", out _));
    }

    [Fact]
    public void GetSchemaSummary_WhenSelectedTablesExceedMaxCountedTables_SkipsRowCounts()
    {
        using var db = new TempDatabaseWithTables(("authors", 2), ("books", 3), ("tmp_old", 1));
        using var explorer = new SqliteExplorer(db.Path);

        var root = Parse(explorer.GetSchemaSummary(new SchemaSummaryOptions
        {
            CountRows = true,
            MaxCountedTables = 2,
        }));

        Assert.True(root.GetProperty("rowCountsSkipped").GetBoolean());
        Assert.False(root.TryGetProperty("rowCounts", out _));
    }

    [Fact]
    public void GetSchemaSummary_WhenSelectedTablesEqualMaxCountedTables_StillCountsRows()
    {
        using var db = new TempDatabaseWithTables(("authors", 2), ("books", 3), ("tmp_old", 1));
        using var explorer = new SqliteExplorer(db.Path);

        var root = Parse(explorer.GetSchemaSummary(new SchemaSummaryOptions
        {
            CountRows = true,
            MaxCountedTables = 3,
        }));

        Assert.False(root.GetProperty("rowCountsSkipped").GetBoolean());
        Assert.Equal(3, root.GetProperty("rowCounts").EnumerateObject().Count());
    }

    [Fact]
    public void GetSchemaSummary_WithSampleRows_ReturnsRowsForEachSelectedTable()
    {
        using var db = new TempDatabaseWithTables(("authors", 2), ("books", 3));
        using var explorer = new SqliteExplorer(db.Path);

        var root = Parse(explorer.GetSchemaSummary(new SchemaSummaryOptions { SampleRows = 1 }));

        var samples = root.GetProperty("samples");
        Assert.Equal(2, samples.EnumerateObject().Count());
        Assert.Equal(1, samples.GetProperty("books").GetProperty("rows").GetArrayLength());
    }

    [Fact]
    public void GetSchemaSummary_WithInvalidPattern_ReturnsErrorPayload()
    {
        using var db = new TempDatabaseWithTables(("books", 1));
        using var explorer = new SqliteExplorer(db.Path);

        var root = Parse(explorer.GetSchemaSummary(new SchemaSummaryOptions { IncludeTables = ["["] }));

        Assert.True(root.TryGetProperty("error", out var error));
        Assert.False(string.IsNullOrWhiteSpace(error.GetString()));
    }

    [Fact]
    public void GetSchemaSummary_NullOptions_ThrowsArgumentNullException()
    {
        using var db = new TempDatabaseWithTables(("books", 1));
        using var explorer = new SqliteExplorer(db.Path);

        Assert.Throws<ArgumentNullException>(() => explorer.GetSchemaSummary(null!));
    }

    [Fact]
    public void CountRows_ReturnsRowCountOfTable()
    {
        using var db = new TempDatabaseWithTables(("books", 3));
        using var explorer = new SqliteExplorer(db.Path);

        Assert.Equal(3, explorer.CountRows("books"));
    }

    [Fact]
    public void CountRows_UnknownTable_ThrowsArgumentException()
    {
        using var db = new TempDatabaseWithTables(("books", 3));
        using var explorer = new SqliteExplorer(db.Path);

        Assert.Throws<ArgumentException>(() => explorer.CountRows("missing"));
    }
}
