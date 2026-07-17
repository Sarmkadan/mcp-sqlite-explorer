namespace McpSqliteExplorer.Tests;

/// <summary>
/// Contains integration tests for the <see cref="SqliteExplorer"/> class.
/// Tests verify that the SQLite database explorer correctly interacts with SQLite databases,
/// including listing tables, describing table schemas, sampling data, and executing SELECT queries.
/// </summary>
public sealed class SqliteExplorerTests
{
    /// <summary>
    /// Tests that <see cref="SqliteExplorer.ListTables"/> returns user-created tables and views
    /// while excluding internal SQLite system tables (those starting with "sqlite_").
    /// </summary>
    [Fact]
    public void ListTables_ReturnsUserTablesAndViews_ButNotSqliteInternals()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var tables = explorer.ListTables();
        var names = tables.Select(t => t.Name).ToList();

        Assert.Contains("authors", names);
        Assert.Contains("books", names);
        Assert.Contains("recent_books", names);
        Assert.DoesNotContain(names, n => n.StartsWith("sqlite_", StringComparison.Ordinal));

        var view = tables.Single(t => t.Name == "recent_books");
        Assert.Equal("view", view.Type);
    }

    /// <summary>
    /// Tests that <see cref="SqliteExplorer.DescribeTable"/> returns correct column metadata
    /// including primary key status and nullability constraints for each column.
    /// </summary>
    [Fact]
    public void DescribeTable_ReturnsColumnMetadata()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var columns = explorer.DescribeTable("authors");

        var id = columns.Single(c => c.Name == "id");
        Assert.True(id.PrimaryKey);

        var name = columns.Single(c => c.Name == "name");
        Assert.True(name.NotNull);
        Assert.False(name.PrimaryKey);

        var country = columns.Single(c => c.Name == "country");
        Assert.False(country.NotNull);
    }

    /// <summary>
    /// Tests that <see cref="SqliteExplorer.DescribeTable"/> throws an <see cref="ArgumentException"/>
    /// when attempting to describe a non-existent table.
    /// </summary>
    [Fact]
    public void DescribeTable_UnknownTable_Throws()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        Assert.Throws<ArgumentException>(() => explorer.DescribeTable("no_such_table"));
    }

    /// <summary>
    /// Tests that <see cref="SqliteExplorer.SampleRows"/> respects the row limit parameter
    /// and returns exactly the requested number of rows.
    /// </summary>
    [Fact]
    public void SampleRows_RespectsLimit()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var result = explorer.SampleRows("books", limit: 2);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(2, result.AppliedRowCap);
        Assert.Contains("title", result.Columns);
    }

    /// <summary>
    /// Tests that <see cref="SqliteExplorer.SampleRows"/> throws an <see cref="ArgumentException"/>
    /// when attempting to sample rows from a non-existent table.
    /// </summary>
    [Fact]
    public void SampleRows_UnknownTable_Throws()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        Assert.Throws<ArgumentException>(() => explorer.SampleRows("ghost"));
    }

    /// <summary>
    /// Tests that <see cref="SqliteExplorer.RunSelect"/> executes a SELECT query and returns
    /// the matching rows with correct column values.
    /// </summary>
    [Fact]
    public void RunSelect_ReturnsMatchingRows()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var result = explorer.RunSelect(
            "SELECT title, year FROM books WHERE author_id = 1 ORDER BY year;");

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("A Wizard of Earthsea", result.Rows[0][0]);
        Assert.False(result.Truncated);
    }

    /// <summary>
    /// Tests that <see cref="SqliteExplorer.RunSelect"/> sets the <see cref="QueryResult.Truncated"/> flag to true
    /// when the query result exceeds the row limit and is truncated.
    /// </summary>
    [Fact]
    public void RunSelect_SetsTruncatedFlag_WhenResultExceedsCap()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        // books has 3 rows; the cap is applied in code, so the flag flips.
        var result = explorer.RunSelect("SELECT * FROM books;", limit: 2);

        Assert.Equal(2, result.Rows.Count);
        Assert.True(result.Truncated);
    }

    /// <summary>
    /// Tests that <see cref="SqliteExplorer.RunSelect"/> allows Common Table Expressions (CTEs)
    /// in SELECT queries and correctly processes them.
    /// </summary>
    [Fact]
    public void RunSelect_WithCte_IsAllowed()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var result = explorer.RunSelect(
            "WITH old AS (SELECT * FROM books WHERE year < 1970) SELECT title FROM old ORDER BY title;");

        // Solaris (1961) and A Wizard of Earthsea (1968) both predate 1970.
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("A Wizard of Earthsea", result.Rows[0][0]);
        Assert.Equal("Solaris", result.Rows[1][0]);
    }

    /// <summary>
    /// Tests that <see cref="SqliteExplorer.RunSelect"/> correctly maps NULL values from the database
    /// to null in the result rows.
    /// </summary>
    [Fact]
    public void RunSelect_NullValues_MapToNull()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var result = explorer.RunSelect("SELECT country FROM authors WHERE id = 3;");

        Assert.Single(result.Rows);
        Assert.Null(result.Rows[0][0]);
    }

    /// <summary>
    /// Tests that <see cref="SqliteExplorer.RunSelect"/> rejects write statements (INSERT, UPDATE, DELETE, etc.)
    /// by throwing an <see cref="ArgumentException"/> before execution.
    /// </summary>
    [Fact]
    public void RunSelect_WriteStatement_IsRejectedBeforeExecution()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        Assert.Throws<ArgumentException>(() =>
            explorer.RunSelect("DELETE FROM books;"));

        // The database must be untouched.
        var result = explorer.RunSelect("SELECT COUNT(*) FROM books;");
        Assert.Equal(3L, Convert.ToInt64(result.Rows[0][0]));
    }

    /// <summary>
    /// Tests that the <see cref="SqliteExplorer"/> constructor throws a <see cref="FileNotFoundException"/>
    /// when initialized with a path to a non-existent database file.
    /// </summary>
    [Fact]
    public void Constructor_MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            new SqliteExplorer("/tmp/definitely-not-a-real-database-file.db"));
    }
}
