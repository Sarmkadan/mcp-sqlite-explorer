
## SqliteToolsExtensionsTests

The SqliteToolsExtensionsTests class contains unit tests for the SqliteToolsExtensions class. It verifies the correctness of various extension methods for SqliteTools, including GetTableCount, GetRowCount, GetAllColumns, and GetSchemaSummary.

Example usage:
```csharp
using McpSqliteExplorer;

var explorer = new SqliteExplorer("/tmp/test.db");

// Get the count of tables in the database
var tableCountJson = SqliteToolsExtensions.GetTableCount(explorer);

// Get the row count of a specific table
var rowCount = SqliteToolsExtensions.GetRowCount(explorer, "users");

// Get all columns across all tables in the database
var allColumns = SqliteToolsExtensions.GetAllColumns(explorer);

// Get a summary of the database schema
var schemaSummary = SqliteToolsExtensions.GetSchemaSummary(explorer);
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


## SqliteExplorerValidationTests

The SqliteExplorerValidationTests class verifies the validation helpers that `SqliteExplorerValidation` exposes as extension methods on `SqliteExplorer` and `TableInfo`: `Validate` collects every problem into a list of messages, `IsValid` answers the same question as a single bool, and `EnsureValid` runs the checks and throws when handed a null instance. The tests cover valid explorers and tables, tables with null, empty or whitespace-only names, and `Type` values that are empty, unknown, mixed case, or one of the supported `table` / `view` kinds.

Example usage demonstrating the validation surface covered by these tests:
```csharp
using McpSqliteExplorer;

var explorer = new SqliteExplorer("path/to/database.db");

// Collect every validation problem found on the explorer instance
var explorerErrors = explorer.Validate();

// Or just ask for a boolean verdict
bool isValid = explorer.IsValid();

// Run the same checks and fail fast when something is wrong
explorer.EnsureValid();

// Validate a single table descriptor, e.g. one returned by the catalog
var table = explorer.ListTables()[0];
var tableErrors = table.Validate();
```
