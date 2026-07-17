namespace McpSqliteExplorer.Tests;

public sealed class SqliteExplorerSchemaTests
{
    [Fact]
    public void ListIndexes_ReturnsExplicitIndexWithColumns()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var indexes = explorer.ListIndexes("books");

        var yearIndex = indexes.Single(i => i.Name == "idx_books_year");
        Assert.False(yearIndex.Unique);
        Assert.Equal("create-index", yearIndex.Origin);
        Assert.False(yearIndex.Partial);
        Assert.Equal(["year"], yearIndex.Columns);
    }

    [Fact]
    public void ListIndexes_TableWithoutIndexes_ReturnsEmpty()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        // authors has only a rowid-aliased INTEGER PRIMARY KEY, which does not
        // materialise as an index in index_list.
        Assert.Empty(explorer.ListIndexes("authors"));
    }

    [Fact]
    public void ListIndexes_UnknownTable_Throws()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        Assert.Throws<ArgumentException>(() => explorer.ListIndexes("ghost"));
    }

    [Fact]
    public void ListForeignKeys_ReturnsDeclaredReferences()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var foreignKeys = explorer.ListForeignKeys("loans");

        var fk = Assert.Single(foreignKeys);
        Assert.Equal("loans", fk.Table);
        Assert.Equal("book_id", fk.Column);
        Assert.Equal("books", fk.ReferencesTable);
        Assert.Equal("id", fk.ReferencesColumn);
        Assert.Equal("CASCADE", fk.OnDelete);
    }

    [Fact]
    public void GetForeignKeyGraph_CoversAllTables()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var edges = explorer.GetForeignKeyGraph();

        Assert.Contains(edges, e => e.Table == "books" && e.ReferencesTable == "authors");
        Assert.Contains(edges, e => e.Table == "loans" && e.ReferencesTable == "books");
    }

    [Fact]
    public void ExploreForeignKeyChain_WalksBothDirections()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var hops = explorer.ExploreForeignKeyChain("books", maxDepth: 2);

        // books -> authors (outgoing) and books <- loans (incoming), both depth 1.
        Assert.Contains(hops, h =>
            h.Depth == 1 && h.ToTable == "authors" && h.Direction == "references");
        Assert.Contains(hops, h =>
            h.Depth == 1 && h.ToTable == "loans" && h.Direction == "referenced-by");
    }

    [Fact]
    public void ExploreForeignKeyChain_DepthOne_DoesNotReachTwoHopNeighbours()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        // loans -> books at depth 1; authors is only reachable at depth 2.
        var hops = explorer.ExploreForeignKeyChain("loans", maxDepth: 1);

        Assert.Contains(hops, h => h.ToTable == "books");
        Assert.DoesNotContain(hops, h => h.ToTable == "authors");
    }

    [Fact]
    public void GenerateErd_ContainsTablesColumnsAndRelationships()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var erd = explorer.GenerateErd();

        Assert.StartsWith("erDiagram", erd);
        Assert.Contains("books {", erd);
        Assert.Contains("INTEGER id PK", erd);
        Assert.Contains("INTEGER author_id FK", erd);
        Assert.Contains("books }o--|| authors", erd);
        Assert.Contains("loans }o--|| books", erd);
        // Views are not entities in an ERD.
        Assert.DoesNotContain("recent_books", erd);
    }

    [Fact]
    public void GetMigrationHistory_ReadsEfHistoryTable()
    {
        using var db = new TestDatabase();
        var explorer = new SqliteExplorer(db.Path);

        var info = explorer.GetMigrationHistory();

        Assert.True(info.HasHistoryTable);
        Assert.Equal(2, info.Migrations.Count);
        Assert.Equal("20250101000000_Initial", info.Migrations[0].MigrationId);
        Assert.Equal("9.0.0", info.Migrations[0].ProductVersion);
    }
}
