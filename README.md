## Getting Started

McpSqliteExplorer is an MCP server (stdio) that opens one SQLite file read-only. It requires the .NET 10 SDK.

### 1. Build

```bash
dotnet build McpSqliteExplorer.slnx
```

### 2. Run

The database path is the first argument, or the `SQLITE_DB_PATH` environment variable. Use an absolute path:

```bash
dotnet run --no-build --project src/McpSqliteExplorer -- /absolute/path/to/app.db
```

The process waits for an MCP client on stdin; stop it with Ctrl+C. Keep `--no-build` and run `dotnet build` first: a plain `dotnet run` that has to compile writes build warnings to stdout, which corrupts the MCP stream.

### 3. Client config

Add the server to `.mcp.json` (Claude Code, project root) or to `mcpServers` in `claude_desktop_config.json` (Claude Desktop). Replace both paths with absolute paths:

```json
{
  "mcpServers": {
    "sqlite-explorer": {
      "command": "dotnet",
      "args": [
        "run",
        "--no-build",
        "--project",
        "/absolute/path/to/mcp-sqlite-explorer/src/McpSqliteExplorer",
        "--",
        "/absolute/path/to/app.db"
      ]
    }
  }
}
```

### 4. First call: `list_tables`

`list_tables` takes no arguments and returns the tables and views (`sqlite_*` internals are hidden). Against a sample `shop.db` with four tables and one view:

```json
{
  "count": 5,
  "tables": [
    { "name": "customers", "type": "table" },
    { "name": "order_items", "type": "table" },
    { "name": "orders", "type": "table" },
    { "name": "products", "type": "table" },
    { "name": "v_order_totals", "type": "view" }
  ]
}
```

Columns come from `describe_table` with `{"table": "orders"}` (trimmed to two of four columns):

```json
{
  "table": "orders",
  "columnCount": 4,
  "columns": [
    { "name": "id", "type": "INTEGER", "notNull": false, "defaultValue": null, "primaryKey": true },
    { "name": "customer_id", "type": "INTEGER", "notNull": true, "defaultValue": null, "primaryKey": false },
    "..."
  ]
}
```

Row counts come from `table_stats` called with no arguments (per-table overview; tables only, no views):

```json
{
  "count": 4,
  "tables": [
    { "table": "customers", "rowCount": 3, "columnCount": 4, "indexCount": 1, "estimatedSizeBytes": null },
    { "table": "order_items", "rowCount": 4, "columnCount": 3, "indexCount": 1, "estimatedSizeBytes": null },
    { "table": "orders", "rowCount": 3, "columnCount": 4, "indexCount": 1, "estimatedSizeBytes": null },
    { "table": "products", "rowCount": 3, "columnCount": 4, "indexCount": 1, "estimatedSizeBytes": null }
  ]
}
```

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

## SqliteToolsExtensions

The SqliteToolsExtensions class provides extension methods for SqliteExplorer to perform common database inspection tasks.
It includes methods to get table and row counts, column information, schema summaries, and to sample table data.

Example usage:
```csharp
using McpSqliteExplorer;

var explorer = new SqliteExplorer("/tmp/test.db");

// Get the count of tables and views in the database
var tableCountJson = explorer.GetTableCount();

// Get the row count of a specific table
var rowCountJson = explorer.GetRowCount("users");

// Get all columns across all tables in the database
var allColumnsJson = explorer.GetAllColumns();

// Get a summary of the database schema
var schemaSummaryJson = explorer.GetSchemaSummary();

// Get a random sample of 5 rows from the 'users' table
var sampleJson = explorer.SampleTable("users", 5);
```

## SqliteToolsTests

The SqliteToolsTests class verifies the static methods of the SqliteTools class, which provide JSON-based inspection capabilities for SQLite databases, including listing tables, describing tables, listing indexes, sampling rows, running select queries, explaining queries, and getting table statistics.

Example usage:
```csharp
using McpSqliteExplorer;

var explorer = new SqliteExplorer("path/to/database.db");

// List all tables and views in the database
var tablesJson = SqliteTools.ListTables(explorer);

// Get the schema for a specific table
var tableJson = SqliteTools.DescribeTable(explorer, "users");

// Get indexes for a table
var indexesJson = SqliteTools.ListIndexes(explorer, "users");

// Sample 5 rows from a table
var sampleJson = SqliteTools.SampleRows(explorer, "users", 5);

// Run a read-only SELECT query
var selectJson = SqliteTools.RunSelect(explorer, "SELECT * FROM users WHERE age > 18");

// Get the query execution plan
var explainJson = SqliteTools.ExplainQuery(explorer, "SELECT * FROM users WHERE age > 18");

// Get statistics for a table
var statsJson = SqliteTools.TableStats(explorer, "users");
```