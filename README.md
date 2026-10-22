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

## Running queries

`run_select` runs one read-only `SELECT` (or `WITH ... SELECT`) statement. Its optional arguments are `limit` (1-1000, default 100) and `timeBudgetSeconds` (default 15).

The examples use the sample `shop.db` (3 customers, 3 products, 3 orders, 4 order items). The JSON shown is what `SqliteValueConverter.QueryResultConverter` writes. Setup, once:

```csharp
using System.Text.Json;
using McpSqliteExplorer;

using var explorer = new SqliteExplorer("/absolute/path/to/shop.db");
var options = SqliteValueConverter.CreateJsonOptions(true, true, new SqliteValueConverter.QueryResultConverter());
```

### Simple SELECT

```csharp
var result = explorer.RunSelect("SELECT id, email, full_name FROM customers ORDER BY id");
Console.WriteLine(JsonSerializer.Serialize(result, options));
```

```json
{
  "columns": [
    "id",
    "email",
    "full_name"
  ],
  "rows": [
    [
      1,
      "ada@example.com",
      "Ada Lovelace"
    ],
    [
      2,
      "alan@example.com",
      "Alan Turing"
    ],
    [
      3,
      "grace@example.com",
      "Grace Hopper"
    ]
  ],
  "appliedRowCap": 100
}
```

The `run_select` tool does not use this converter. An MCP client receives a different envelope, with each row as an object keyed by column name (abbreviated):

```json
{
  "columns": ["id", "email", "full_name"],
  "rowCount": 3,
  "appliedRowCap": 100,
  "truncated": false,
  "timedOut": false,
  "timeoutMessage": null,
  "rowsBeforeTimeout": 0,
  "rows": [
    { "id": 1, "email": "ada@example.com", "full_name": "Ada Lovelace" }
  ]
}
```

### Limiting rows (no query parameters)

`RunSelect` takes raw SQL and binds no parameter values, so put literals directly in the SQL. The `limit` argument is the only value passed alongside the statement. Here it caps the result at two rows, and `truncated` becomes `true` because a third row exists:

```csharp
var result = explorer.RunSelect("SELECT id, full_name FROM customers ORDER BY id", limit: 2);
Console.WriteLine(JsonSerializer.Serialize(result, options));
```

```json
{
  "columns": [
    "id",
    "full_name"
  ],
  "rows": [
    [
      1,
      "Ada Lovelace"
    ],
    [
      2,
      "Alan Turing"
    ]
  ],
  "appliedRowCap": 2,
  "truncated": true
}
```

### NULL and BLOB values

```csharp
var result = explorer.RunSelect("SELECT id, NULL AS missing, X'0102FF' AS payload, zeroblob(2000) AS big FROM customers WHERE id = 1");
Console.WriteLine(JsonSerializer.Serialize(result, options));
```

```json
{
  "columns": [
    "id",
    "missing",
    "payload",
    "big"
  ],
  "rows": [
    [
      1,
      null,
      "AQL/",
      "blob(2000 bytes)"
    ]
  ],
  "appliedRowCap": 100
}
```

`NULL` becomes `null`. A BLOB is base64 encoded, except when it is longer than 1024 bytes (`SqliteValueConverter.MaxBlobInlineSize`); then it becomes a `blob(N bytes)` placeholder.

### Limitations

- **Read-only.** The database is opened with `Mode=ReadOnly`. A call must contain exactly one statement that starts with `SELECT` or `WITH`. `INSERT`, `UPDATE`, `DELETE`, `REPLACE`, `DROP`, `ALTER`, `CREATE`, `TRUNCATE`, `ATTACH`, `DETACH`, `REINDEX`, `VACUUM` and `PRAGMA` are refused by a keyword scan. The scan also refuses those words as separate tokens inside string literals, so `SELECT 'a update b'` is rejected.
- **Row cap.** An omitted or non-positive `limit` means 100. A `limit` above 1000 is silently clamped to 1000. Rows past the cap are dropped and `truncated` is `true`.
- **No query parameters.** The SQL is sent as written, and there is no argument for bound values.
- **Value types.** Values come back as SQLite stores them: INTEGER and REAL as numbers, TEXT as strings (dates are TEXT), BLOB as base64 strings. `SELECT 1e999` (Infinity) makes `run_select` return an error payload, while the converter writes it as the string `"Infinity"`. BLOBs over 1024 bytes become `blob(N bytes)` in the converter but are returned in full, base64 encoded, by `run_select`.

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