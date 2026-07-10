namespace McpSqliteExplorer.Tests;

public sealed class SqliteExplorerTests
{
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

    [Fact]
    public void DescribeTable_UnknownTable_Throws()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        Assert.Throws<ArgumentException>(() => explorer.DescribeTable("no_such_table"));
    }

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

    [Fact]
    public void SampleRows_UnknownTable_Throws()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        Assert.Throws<ArgumentException>(() => explorer.SampleRows("ghost"));
    }

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

    [Fact]
    public void RunSelect_NullValues_MapToNull()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var result = explorer.RunSelect("SELECT country FROM authors WHERE id = 3;");

        Assert.Single(result.Rows);
        Assert.Null(result.Rows[0][0]);
    }

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

    [Fact]
    public void Constructor_MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            new SqliteExplorer("/tmp/definitely-not-a-real-database-file.db"));
    }
}
