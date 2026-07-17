using Microsoft.Data.Sqlite;

namespace McpSqliteExplorer.Tests;

public sealed class SqliteExplorerAnalysisTests
{
    [Fact]
    public void ExplainQueryPlan_IndexedLookup_UsesIndex()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var plan = explorer.ExplainQueryPlan("SELECT * FROM books WHERE year = 1974;");

        Assert.NotEmpty(plan);
        Assert.Contains(plan, n =>
            n.Detail.Contains("idx_books_year", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExplainQueryPlan_UnindexedFilter_ReportsScan()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var plan = explorer.ExplainQueryPlan("SELECT * FROM books WHERE title = 'Solaris';");

        Assert.Contains(plan, n =>
            n.Detail.StartsWith("SCAN", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExplainQueryPlan_WriteStatement_IsRejected()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        Assert.Throws<ArgumentException>(() =>
            explorer.ExplainQueryPlan("DELETE FROM books;"));
    }

    [Fact]
    public void ExplainQueryPlan_BadColumn_GetsActionableMessage()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            explorer.ExplainQueryPlan("SELECT nope FROM books;"));

        Assert.Contains("describe_table", ex.Message);
    }

    [Fact]
    public void ProfileTable_ComputesNullRatesAndCardinality()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var profile = explorer.ProfileTable("authors");

        Assert.Equal(3, profile.RowCount);

        var country = profile.Columns.Single(c => c.Name == "country");
        Assert.Equal(1, country.NullCount);
        Assert.Equal(0.3333, country.NullRate);
        Assert.Equal(2, country.DistinctCount);
        Assert.Equal("PL", country.Min);
        Assert.Equal("US", country.Max);

        var name = profile.Columns.Single(c => c.Name == "name");
        Assert.Equal(0, name.NullCount);
        Assert.Equal(3, name.DistinctCount);
    }

    [Fact]
    public void ProfileTable_ReportsTopValues()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var profile = explorer.ProfileTable("books");

        var authorId = profile.Columns.Single(c => c.Name == "author_id");
        // author 1 wrote two of the three books, so it tops the frequency list.
        Assert.Equal(1L, authorId.TopValues[0].Value);
        Assert.Equal(2, authorId.TopValues[0].Occurrences);
    }

    [Fact]
    public void GetTableStats_CountsRowsColumnsAndIndexes()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var stats = explorer.GetTableStats();

        var books = stats.Single(s => s.Table == "books");
        Assert.Equal(3, books.RowCount);
        Assert.Equal(4, books.ColumnCount);
        Assert.Equal(1, books.IndexCount);

        // Views are not tables; they carry no stats entry.
        Assert.DoesNotContain(stats, s => s.Table == "recent_books");
    }

    [Fact]
    public void SuggestIndexes_ProposesIndexForScannedColumn()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var suggestions = explorer.SuggestIndexes(
            "SELECT * FROM books WHERE title = 'Solaris';");

        var suggestion = Assert.Single(suggestions);
        Assert.Equal("books", suggestion.Table);
        Assert.Contains("title", suggestion.Columns);
        Assert.Contains("CREATE INDEX", suggestion.ProposedSql);
    }

    [Fact]
    public void SuggestIndexes_IndexedQuery_ReturnsNothing()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var suggestions = explorer.SuggestIndexes(
            "SELECT * FROM books WHERE year = 1974;");

        Assert.Empty(suggestions);
    }

    [Fact]
    public void GetMigrationHistory_NonEfDatabase_ReportsAbsence()
    {
        var path = Path.Combine(
            Path.GetTempPath(), $"mcp-sqlite-test-{Guid.NewGuid():N}.db");
        try
        {
            using (var connection = new SqliteConnection($"Data Source={path}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE plain (id INTEGER PRIMARY KEY);";
                command.ExecuteNonQuery();
            }

            var explorer = new SqliteExplorer(path);
            var info = explorer.GetMigrationHistory();

            Assert.False(info.HasHistoryTable);
            Assert.Empty(info.Migrations);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}
