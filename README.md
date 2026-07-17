# mcp-sqlite-explorer

A small [Model Context Protocol](https://modelcontextprotocol.io) server that lets an
agent **explore a SQLite database read-only**. Point it at a `.db` file and it exposes
tools to list tables, describe schemas, peek at sample rows, run a capped `SELECT` -
plus a set of analysis tools: ERD generation, foreign-key graph walking, index and
query-plan inspection, per-column data profiling, table size stats, heuristic index
suggestions and EF Core migration history. Nothing it does can modify the database.

Written in C# (.NET 10) on top of the official
[`ModelContextProtocol`](https://www.nuget.org/packages/ModelContextProtocol) SDK and
`Microsoft.Data.Sqlite`.

## Why read-only matters here

Giving a language model a raw SQL connection is a footgun: one hallucinated `DELETE`
or `DROP` and the data is gone. This server closes that off in two independent layers:

1. **The connection is opened in `Mode=ReadOnly`.** SQLite itself refuses any write —
   even if a bug slipped a mutating statement through, the engine rejects it.
2. **Statements are validated before they run.** `run_select` only accepts a single
   `SELECT` (or `WITH ... SELECT`) statement. Batched statements, DDL, DML, `PRAGMA`,
   `ATTACH`, `VACUUM`, and writes hidden inside a CTE are all rejected with a clear
   message instead of being sent to the engine.

Every result set is also **row-capped** (default 100, hard maximum 1000) so a careless
`SELECT * FROM huge_table` can't flood the agent's context window. When a result is
truncated the response says so (`"truncated": true`).

## Tools

| Tool | Arguments | Returns |
|------|-----------|---------|
| `list_tables` | – | User tables and views (internal `sqlite_*` objects hidden) |
| `describe_table` | `table` | Columns: name, declared type, nullability, default, PK flag |
| `sample_rows` | `table`, `limit?` | Up to `limit` rows from the table |
| `run_select` | `sql`, `limit?` | Rows for a single read-only `SELECT` / `WITH ... SELECT` |

`limit` defaults to 100 and is clamped to the range 1–1000.

### Schema exploration

| Tool | Arguments | Returns |
|------|-----------|---------|
| `list_indexes` | `table` | Indexes: name, uniqueness, origin (explicit / UNIQUE / PK), partial flag, columns |
| `list_foreign_keys` | `table` | FKs: column, referenced table/column, ON UPDATE / ON DELETE actions |
| `foreign_key_graph` | – | Every FK in the database as a flat edge list |
| `foreign_key_chain` | `table`, `maxDepth?` | BFS over the FK graph from a table, both directions, up to `maxDepth` hops (default 3) |
| `generate_erd` | – | The whole schema as a Mermaid `erDiagram` (tables, typed columns, PK/FK markers, relationships) |
| `migration_history` | – | Applied EF Core migrations from `__EFMigrationsHistory`, or `hasHistoryTable: false` |

### Analysis

| Tool | Arguments | Returns |
|------|-----------|---------|
| `explain_query_plan` | `sql` | `EXPLAIN QUERY PLAN` tree for a SELECT - scans, index usage, join order. The query itself is not executed |
| `profile_table` | `table` | Per column: null count/rate, distinct cardinality, min/max, top-5 most frequent values. Aggregates run inside SQLite, so large tables are fine |
| `table_stats` | – | Per table: row count, column count, index count, on-disk size (when the build exposes `dbstat`) |
| `suggest_indexes` | `sql` | Full-table scans found in the plan, cross-referenced with the query's un-indexed columns, phrased as `CREATE INDEX` DDL for a human to review |

Example - ask why a query is slow and what to do about it:

```
explain_query_plan  sql: "SELECT * FROM orders WHERE customer_email = 'a@b.c'"
→ { "nodes": [ { "detail": "SCAN orders" } ] }

suggest_indexes     sql: "SELECT * FROM orders WHERE customer_email = 'a@b.c'"
→ { "suggestions": [ {
      "table": "orders",
      "columns": ["customer_email"],
      "proposedSql": "CREATE INDEX \"idx_orders_customer_email\" ON \"orders\" (\"customer_email\");",
      "rationale": "The plan shows a full scan of 'orders' (SCAN orders) and the query references these un-indexed columns. ..."
  } ] }
```

The suggestions are deliberately heuristic and the server can never create the index
itself - it hands the DDL to whoever owns the schema.

Example - visualise the schema:

```
generate_erd
→ { "format": "mermaid", "diagram": "erDiagram\n    authors {\n        INTEGER id PK\n ..." }
```

Paste the `diagram` string into any Mermaid renderer (GitHub markdown, mermaid.live,
most IDEs) to get the picture.

### Error messages

Raw SQLite errors are translated into actionable ones: `no such table: ordrs` becomes
"... Use list_tables to see the tables and views that exist in this database", a locked
database explains that a writer holds the lock, `file is not a database` points at
encryption/corruption instead of a bare error code.

## Running it

The database path is the first CLI argument, or the `SQLITE_DB_PATH` environment
variable:

```bash
dotnet run --project src/McpSqliteExplorer -- /path/to/app.db
# or
SQLITE_DB_PATH=/path/to/app.db dotnet run --project src/McpSqliteExplorer
```

The server speaks MCP over **stdio**, so it is normally launched by an MCP client rather
than by hand. Logs go to stderr (stdout is reserved for protocol frames).

### Wiring it into an MCP client

Build a self-contained binary once:

```bash
dotnet publish src/McpSqliteExplorer -c Release -o ./publish
```

Then register it. Example client config:

```json
{
  "mcpServers": {
    "sqlite-explorer": {
      "command": "dotnet",
      "args": ["/abs/path/to/publish/McpSqliteExplorer.dll", "/abs/path/to/app.db"]
    }
  }
}
```

## Project layout

```
McpSqliteExplorer.slnx
src/McpSqliteExplorer/
  Program.cs           # host setup, stdio transport, arg/env handling
  SqliteExplorer.cs    # read-only data access + the SELECT-only guard (the core)
  SqliteExplorerSchema.cs    # indexes, FKs, ERD, EF migration history
  SqliteExplorerAnalysis.cs  # query plans, profiling, table stats, index suggestions
  SqliteTools.cs       # MCP tool surface, thin JSON adapters over SqliteExplorer
  SqliteAnalysisTools.cs     # MCP tool surface for the schema/analysis tools
tests/McpSqliteExplorer.Tests/
  TestDatabase.cs      # temp-file fixture with a small seeded schema
  SqliteExplorerTests.cs
  SqlGuardTests.cs     # the write-rejection / row-cap rules
```

The interesting logic lives in `SqliteExplorer` and is deliberately independent of the
MCP layer, so it can be unit-tested directly without spinning up a server.

## Development

```bash
dotnet build      # build everything
dotnet test       # run the unit tests
```

## SqliteAnalysisTools

The `SqliteAnalysisTools` class exposes read-only analysis utilities that help understand and optimize SQLite schemas. It provides methods to inspect query plans, profile table data, generate statistics, suggest indexes, and visualize relationships without ever modifying the database.

### Usage example

```csharp
using McpSqliteExplorer;

var dbPath = "/path/to/your/database.db";
var tools = new SqliteAnalysisTools(dbPath);

// Generate an Entity-Relationship Diagram for the whole schema
var erd = await tools.GenerateErd();
Console.WriteLine(erd);

// Profile a specific table to understand data distribution
var profile = await tools.ProfileTable("users");
Console.WriteLine($"Null rate for Email: {profile.Columns["Email"].NullRate:P1}");

// Get table statistics including on-disk size
var stats = await tools.TableStatsOverview();
Console.WriteLine($"Total size: {stats.TotalSizeBytes:N0} bytes");

// Suggest indexes for a slow query
var suggestions = await tools.SuggestIndexes(
    "SELECT * FROM orders WHERE customer_email = 'a@b.c'");
foreach (var idx in suggestions)
{
    Console.WriteLine(idx.ProposedSql);
}

// Walk the foreign-key graph from a table
var chain = await tools.ForeignKeyChain("orders", maxDepth: 2);
Console.WriteLine($"Found {chain.Tables.Count} related tables");
```

## SqliteExplorer

The `SqliteExplorer` class is the core analysis engine that provides read-only database exploration capabilities. It offers methods to examine query execution plans, profile table data distributions, gather table statistics, and suggest performance improvements through indexes. All operations are performed on a read-only connection, ensuring the database remains unmodified.

### Usage example

```csharp
using McpSqliteExplorer;

var dbPath = "/path/to/your/database.db";
var explorer = new SqliteExplorer(dbPath);

// Analyze a query's execution plan without executing it
var plan = explorer.ExplainQueryPlan(
    "SELECT * FROM orders WHERE customer_email = 'alice@example.com'");
foreach (var node in plan)
{
    Console.WriteLine($"Node {node.Id} (parent {node.Parent}): {node.Detail}");
}

// Profile a table to understand data distribution and quality
var profile = explorer.ProfileTable("users");
Console.WriteLine($"Table {profile.Table} has {profile.RowCount} rows");
Console.WriteLine($"Null rate for Email: {profile.Columns.First(c => c.Name == "Email").NullRate:P1}");

// Get comprehensive statistics for all tables
var stats = explorer.GetTableStats();
foreach (var tableStat in stats)
{
    Console.WriteLine($"{tableStat.Table}: {tableStat.RowCount:N0} rows, " +
                     $"{tableStat.EstimatedSizeBytes?.ToString("N0") ?? "?"} bytes");
}

// Receive index suggestions for slow queries
var suggestions = explorer.SuggestIndexes(
    "SELECT * FROM orders WHERE customer_id = 42 AND order_date > '2024-01-01'");
foreach (var suggestion in suggestions)
{
    Console.WriteLine($"Index suggestion for {suggestion.Table}: {suggestion.ProposedSql}");
    Console.WriteLine($"  Rationale: {suggestion.Rationale}");
}
```

## SqliteExplorerValidation

The `SqliteExplorerValidation` static class provides validation extension methods for the core record types returned by `SqliteExplorer` operations. It helps ensure data integrity by validating that records like `TableInfo`, `ColumnInfo`, and `QueryResult` contain valid, non-null values before they are used in downstream processing or serialization.

### Usage example

```csharp
using McpSqliteExplorer;

var dbPath = "/path/to/your/database.db";
var explorer = new SqliteExplorer(dbPath);

// Get table information
var tables = explorer.ListTables();

// Validate each table info record before using it
foreach (var table in tables)
{
    var validationErrors = table.Validate();
    if (validationErrors.Any())
    {
        Console.WriteLine($"Table {table.Name} has validation errors:");
        foreach (var error in validationErrors)
        {
            Console.WriteLine($"  - {error}");
        }
    }
    else
    {
        Console.WriteLine($"Table {table.Name} is valid");
    }
}

// Use the convenience methods for quick validation checks
if (!tables.First().IsValid())
{
    Console.WriteLine("First table is invalid!");
}

// Throw an exception if validation fails
tables.First().EnsureValid();

// Validate query results before processing
var result = explorer.RunSelect("SELECT * FROM users LIMIT 10");
result.EnsureValid(); // Throws if result contains invalid data
```

## SqliteExplorerExtensions

The `SqliteExplorerExtensions` static class provides a set of convenient extension methods that simplify common database operations and reduce boilerplate code when working with `SqliteExplorer` and `QueryResult` objects. These methods handle type conversion, counting, existence checks, and pattern matching for table names.

### Usage example

```csharp
using McpSqliteExplorer;

var dbPath = "/path/to/your/database.db";
var explorer = new SqliteExplorer(dbPath);

// Count rows in a table without fetching all data
var userCount = explorer.CountRows("SELECT * FROM users");
Console.WriteLine($"Users table has {userCount} rows");

// Check if a table has any rows
if (explorer.HasRows("orders"))
{
    Console.WriteLine("Orders table is not empty");
}

// Get the first value from a query result
var firstUserId = explorer.RunSelect("SELECT id FROM users LIMIT 1")
    .FirstValue<int?>();
Console.WriteLine($"First user ID: {firstUserId}");

// Get the first value with automatic type conversion
var firstUserName = explorer.RunSelect("SELECT name FROM users LIMIT 1")
    .FirstValueAs<string>();
Console.WriteLine($"First user name: {firstUserName}");

// Find tables matching a pattern (supports * and ? wildcards)
var userTables = explorer.GetTablesMatching("user*");
foreach (var table in userTables)
{
    Console.WriteLine($"Found table: {table}");
}
```

## SqliteExplorerAnalysisTests

The `SqliteExplorerAnalysisTests` class contains integration tests that verify the correctness of SQLite database analysis features. It tests query plan analysis, table profiling, statistics gathering, index suggestions, and migration history detection to ensure all analysis tools work as expected.


### Usage example

```csharp
using McpSqliteExplorer;
using McpSqliteExplorer.Tests;

// Create a test database fixture
using var db = new TestDatabase();
var explorer = new SqliteExplorer(db.Path);

// Test that ExplainQueryPlan correctly identifies index usage
var plan = explorer.ExplainQueryPlan("SELECT * FROM books WHERE year = 1974;");
Assert.Contains(plan, n => n.Detail.Contains("idx_books_year", StringComparison.OrdinalIgnoreCase));

// Test that ExplainQueryPlan detects full table scans
var scanPlan = explorer.ExplainQueryPlan("SELECT * FROM books WHERE title = 'Solaris'");
Assert.Contains(scanPlan, n => n.Detail.StartsWith("SCAN", StringComparison.OrdinalIgnoreCase));

// Test that ProfileTable computes null rates and cardinality
var profile = explorer.ProfileTable("authors");
Assert.Equal(3, profile.RowCount);
var country = profile.Columns.Single(c => c.Name == "country");
Assert.Equal(1, country.NullCount);
Assert.Equal(0.3333, country.NullRate);

// Test that GetTableStats counts rows, columns, and indexes
var stats = explorer.GetTableStats();
var books = stats.Single(s => s.Table == "books");
Assert.Equal(3, books.RowCount);
Assert.Equal(4, books.ColumnCount);
Assert.Equal(1, books.IndexCount);

// Test that SuggestIndexes proposes indexes for unindexed columns
var suggestions = explorer.SuggestIndexes("SELECT * FROM books WHERE title = 'Solaris'")
    .ToList();
if (suggestions.Any())
{
    var suggestion = suggestions.First();
    Assert.Equal("books", suggestion.Table);
    Assert.Contains("title", suggestion.Columns);
    Assert.Contains("CREATE INDEX", suggestion.ProposedSql);
}

// Test that GetMigrationHistory detects EF Core migration history
var info = explorer.GetMigrationHistory();
if (info.HasHistoryTable)
{
    Console.WriteLine($"Found {info.Migrations.Count} migrations");
}
```

## SqlGuardTestsExtensions

The `SqlGuardTestsExtensions` static class provides a collection of extension methods for `SqlGuardTests` that generate test data for validating SQL guard behavior. These methods return pre-defined lists of SQL statements categorized by their expected behavior (allowed reads, rejected writes, CTE writes, limit clamping, empty statements, and multi-statements), making it easy to write comprehensive tests for SQL statement validation and guard clause functionality.


### Usage example

```csharp
using McpSqliteExplorer.Tests;

// Create a guard instance for extension methods
var guard = new SqlGuardTests();

// Get allowed read statements for testing select-only guards
var allowedReads = guard.GetAllowedReadStatements();
Console.WriteLine($"Found {allowedReads.Count} allowed read statements");
foreach (var statement in allowedReads)
{
    Console.WriteLine($"  - {statement}");
}

// Get rejected write statements for testing guard rejection
var rejectedWrites = guard.GetRejectedWriteStatements();
Console.WriteLine($"Found {rejectedWrites.Count} rejected write statements");
foreach (var statement in rejectedWrites)
{
    Console.WriteLine($"  - {statement}");
}

// Get statements with CTE-based writes that should be rejected
var rejectedCteWrites = guard.GetRejectedCteWriteStatements();
Console.WriteLine($"Found {rejectedCteWrites.Count} CTE write statements");
foreach (var statement in rejectedCteWrites)
{
    Console.WriteLine($"  - {statement}");
}

// Get statements with various limit values for testing clamp functionality
var limitStatements = guard.GetLimitStatements();
Console.WriteLine($"Found {limitStatements.Count} limit statements");
foreach (var (statement, original, clamped) in limitStatements)
{
    Console.WriteLine($"  - {statement} (original: {original}, clamped: {clamped})");
}

// Get empty statements that should be rejected
var emptyStatements = guard.GetEmptyStatements();
Console.WriteLine($"Found {emptyStatements.Count} empty statements");
foreach (var statement in emptyStatements)
{
    Console.WriteLine($"  - '{statement}'");
}

// Get multi-statements that should be rejected
var multiStatements = guard.GetMultiStatements();
Console.WriteLine($"Found {multiStatements.Count} multi-statements");
foreach (var statement in multiStatements)
{
    Console.WriteLine($"  - {statement}");
}
```

## SqliteExplorerTestsExtensions

The `SqliteExplorerTestsExtensions` static class provides extension methods for `SqliteExplorerTests` that simplify common test scenarios such as creating test databases, extracting values from query results, and asserting table states. These extension methods provide fluent APIs for common test operations in the test suite.

### Usage example

```csharp
using McpSqliteExplorer;
using McpSqliteExplorer.Tests;

// Create a test instance and create a test database
var tests = new SqliteExplorerTests();
var (explorer, dbPath) = tests.CreateTestDatabase();

// Use the explorer to run queries
var result = explorer.RunSelect("SELECT COUNT(*) FROM Users");

// Get a value from the result using the extension method
int userCount = tests.GetValue<int>(result, 0, "");

// Assert that a table has a specific number of rows
tests.AssertTableRowCount(explorer, "Users", 5);

// Assert that a specific column in the first row has an expected value
tests.AssertFirstRowValue<string>(explorer, "Users", "Name", "John Doe");
```

## TestDatabaseJsonExtensions

The `TestDatabaseJsonExtensions` static class provides JSON serialization and deserialization utilities for the `TestDatabase` class. It enables round-trip serialization of test database instances to JSON strings and back, including custom handling of the `SqliteConnectionStringBuilder` property through a dedicated JSON converter. This is particularly useful for persisting test database state or transferring it between test runs.



### Usage example

```csharp
using McpSqliteExplorer.Tests;
using Microsoft.Data.Sqlite;

// Create a test database instance
using var db = new TestDatabase();

// Serialize the test database to a compact JSON string
var json = db.ToJson();
Console.WriteLine(json);

// Serialize with indentation for readability
var prettyJson = db.ToJson(indented: true);
Console.WriteLine(prettyJson);

// Deserialize from JSON back to a TestDatabase instance
var dbPath = "/tmp/test.db";
var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath };
var jsonWithConnection = $"{{ \"Path\": \"{dbPath}\", \"ConnectionString\": \"{connectionString}\"}};";
var restoredDb = TestDatabaseJsonExtensions.FromJson(jsonWithConnection);

// Attempt to deserialize with error handling
if (TestDatabaseJsonExtensions.TryFromJson(json, out var deserializedDb))
{
    Console.WriteLine("Successfully deserialized test database");
}
else
{
    Console.WriteLine("Failed to deserialize test database");
}
```

## License

MIT — see [LICENSE](LICENSE).
