using Microsoft.Data.Sqlite;

namespace McpSqliteExplorer.Tests;

/// <summary>
/// Contains integration tests for SQLite database analysis features provided by the SqliteExplorer class.
/// Tests cover query plan analysis, table profiling, statistics gathering, index suggestions,
/// and migration history detection.
/// </summary>
public sealed class SqliteExplorerAnalysisTests
{
    [Fact]
    /// <summary>
    /// Tests that ExplainQueryPlan correctly identifies when a query uses an index for indexed column lookups.
    /// Verifies that the query plan contains the expected index name for a WHERE clause on an indexed column.
    /// </summary>
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
    /// <summary>
    /// Tests that ExplainQueryPlan correctly identifies full table scans for queries on unindexed columns.
    /// Verifies that the query plan reports SCAN operations when filtering on columns without indexes.
    /// </summary>
    public void ExplainQueryPlan_UnindexedFilter_ReportsScan()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var plan = explorer.ExplainQueryPlan("SELECT * FROM books WHERE title = 'Solaris';");

        Assert.Contains(plan, n =>
            n.Detail.StartsWith("SCAN", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    /// <summary>
    /// Tests that ExplainQueryPlan rejects write statements and throws ArgumentException.
    /// Verifies that the method properly validates input and prevents potentially destructive operations.
    /// </summary>
    public void ExplainQueryPlan_WriteStatement_IsRejected()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        Assert.Throws<ArgumentException>(() =>
            explorer.ExplainQueryPlan("DELETE FROM books;"));
    }

    [Fact]
    /// <summary>
    /// Tests that ExplainQueryPlan provides actionable error messages for invalid column references.
    /// Verifies that when querying a non-existent column, the exception message contains helpful
    /// information about the table structure and available columns.
    /// </summary>
    public void ExplainQueryPlan_BadColumn_GetsActionableMessage()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            explorer.ExplainQueryPlan("SELECT nope FROM books;"));

        Assert.Contains("describe_table", ex.Message);
    }

    [Fact]
    /// <summary>
    /// Tests that ProfileTable correctly computes null rates, cardinality, and value statistics for table columns.
    /// Verifies that the method accurately counts rows, null values, distinct values, and computes min/max
    /// for string columns when profiling a table.
    /// </summary>
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
    /// <summary>
    /// Tests that ProfileTable correctly identifies and reports the most frequent values for a column.
    /// Verifies that the method returns top values sorted by frequency and includes both the value
    /// and the number of occurrences for each top value.
    /// </summary>
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
    /// <summary>
    /// Tests that GetTableStats correctly counts rows, columns, and indexes for each table.
    /// Verifies that the method returns accurate statistics including row count, column count,
    /// and index count for each table in the database, and excludes views from the results.
    /// </summary>
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
    /// <summary>
    /// Tests that SuggestIndexes proposes an index for a column that causes a full table scan.
    /// Verifies that when a query filters on an unindexed column, the method returns a suggestion
    /// containing the table name, column name, and the proposed CREATE INDEX SQL statement.
    /// </summary>
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
    /// <summary>
    /// Tests that SuggestIndexes returns no suggestions for queries that already use indexes.
    /// Verifies that when a query filters on an indexed column, the method returns an empty collection
    /// indicating that no additional indexes are needed.
    /// </summary>
    public void SuggestIndexes_IndexedQuery_ReturnsNothing()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var suggestions = explorer.SuggestIndexes(
            "SELECT * FROM books WHERE year = 1974;");

        Assert.Empty(suggestions);
    }

    [Fact]
    /// <summary>
    /// Tests that GetMigrationHistory correctly detects the absence of migration history in non-Entity Framework databases.
    /// Verifies that when a database doesn't contain EF Core migration history tables, the method returns a history info
    /// object with HasHistoryTable set to false and an empty migrations collection.
    /// </summary>
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
