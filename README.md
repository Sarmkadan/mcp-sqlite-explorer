
## SqliteToolsFormattingTests

The SqliteToolsFormattingTests class contains tests for the SqliteToolsFormatting class. It checks the formatting of Sqlite database tables.

Example usage:
```csharp
public SqliteToolsFormattingTests
public void Dispose
public void ListTables_EmptyDatabase_ReturnsEmptyList
public void DescribeTable_TableWithAllColumnTypes_ReturnsCorrectDescription
public void SampleRows_TableWithRows_RowCapEnforced
```

## SqliteAnalysisToolsTests

The SqliteAnalysisToolsTests class verifies the read-only analysis surface provided by the SqliteAnalysisTools class. It ensures that various analysis tools, such as ERD generation, query plan explanation, and table statistics, return correct JSON results when executed against a populated SQLite database.

Example usage demonstrating SqliteAnalysisTools functionality:
```csharp
// Example using SqliteAnalysisTools as validated by SqliteAnalysisToolsTests
using McpSqliteExplorer;

var explorer = new SqliteExplorer("path/to/database.db");

// Explain a query plan
string planJson = SqliteAnalysisTools.ExplainQueryPlan(explorer, "SELECT * FROM users WHERE id = 1");

// Generate an ERD in Mermaid format
string erdJson = SqliteAnalysisTools.GenerateErd(explorer);

// Get table statistics
string statsJson = SqliteAnalysisTools.TableStatsOverview(explorer);
```


## SchemaEdgeCaseTests

The SchemaEdgeCaseTests class contains comprehensive edge case tests for SqliteExplorer schema-related functionality, including handling of tables with unusual names, composite primary keys, and complex foreign key relationships. It ensures robust behavior when dealing with views, indexes on unusual tables, ERD generation, and SQLite migration history detection.

Example usage demonstrating the test scenarios:
```csharp
// Example demonstrating the scenarios covered by SchemaEdgeCaseTests
using McpSqliteExplorer;
using McpSqliteExplorer.Tests;

// Typically these are used within [Fact] methods in the test suite
[Fact]
public void TestSchemaCapabilities()
{
    var explorer = new SqliteExplorer("path/to/database.db");

    // Verify indexing behavior for tables and views
    var indexes = explorer.ListIndexes("books");
    var viewIndexes = explorer.ListIndexes("recent_books"); // Should be empty

    // Verify foreign key relationship analysis
    var foreignKeys = explorer.ListForeignKeys("loans");
    var fkGraph = explorer.GetForeignKeyGraph();
    
    // Explore foreign key chains
    var hops = explorer.ExploreForeignKeyChain("authors", maxDepth: 2);

    // Generate and validate ERD
    string erd = explorer.GenerateErd();

    // Analyze migration history and table types
    var migrationInfo = explorer.GetMigrationHistory();
    var tables = explorer.ListTables();
    var tableDescription = explorer.DescribeTable("books");
}
```

## SqliteExplorerJsonExtensionsTests

The `SqliteExplorerJsonExtensionsTests` class contains unit tests that verify the JSON serialization and deserialization extension methods for `SqliteExplorer`. It checks correct handling of valid explorers, formatting options, and edge cases such as null, empty, whitespace, or malformed JSON inputs.

Example usage:
```csharp
using McpSqliteExplorer.Tests;

var jsonTests = new SqliteExplorerJsonExtensionsTests();

jsonTests.ToJson_WithValidExplorer_ReturnsValidJsonString();
jsonTests.ToJson_WithIndentedTrue_ReturnsFormattedJson();
jsonTests.ToJson_WithIndentedFalse_ReturnsCompactJson();
jsonTests.ToJson_WithNullValue_ThrowsArgumentNullException();
jsonTests.FromJson_WithNullJson_ThrowsArgumentNullException();
jsonTests.TryFromJson_WithInvalidJson_ReturnsFalseAndNull();
```
