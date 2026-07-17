# SqliteAnalysisTools

Utility class providing schema exploration, analysis, and diagnostic helpers for SQLite databases. These methods surface raw SQLite metadata (indexes, foreign keys, query plans, etc.) in human-readable or graph-oriented formats, and expose high-level diagnostics such as index suggestions and table statistics.

## API

### `public static string ListIndexes(SqliteConnection conn, string? tableName = null)`

Returns a formatted list of all indexes in the database, optionally filtered to a specific table. Indexes are grouped by table, with columns, uniqueness, and partial expression shown where applicable. Throws `ArgumentNullException` if `conn` is null.

### `public static string ListForeignKeys(SqliteConnection conn, string? fromTable = null, string? toTable = null)`

Returns a formatted list of foreign key constraints. Optional filters restrict output to keys involving a specific source (`fromTable`) or target (`toTable`) table. Throws `ArgumentNullException` if `conn` is null.

### `public static string ForeignKeyGraph(SqliteConnection conn)`

Generates a Graphviz DOT representation of the database’s foreign key topology. Nodes represent tables; directed edges represent foreign key relationships. Useful for visualizing referential integrity and spotting cycles. Throws `ArgumentNullException` if `conn` is null.

### `public static string ForeignKeyChain(SqliteConnection conn, string startTable, string endTable)`

Returns a shortest-path sequence of foreign key links from `startTable` to `endTable`, or an empty string if no such path exists. The path is expressed as a space-separated list of table names. Throws `ArgumentNullException` if `conn` is null; throws `InvalidOperationException` if either table does not exist.

### `public static string GenerateErd(SqliteConnection conn)`

Produces a compact Entity–Relationship Diagram (ERD) in Graphviz DOT format that includes tables, columns, primary keys, foreign keys, and index hints. Suitable for rendering with Graphviz tools. Throws `ArgumentNullException` if `conn` is null.

### `public static string ExplainQueryPlan(SqliteConnection conn, string sql)`

Runs SQLite’s `EXPLAIN QUERY PLAN` on the provided SQL and returns the formatted plan output. Useful for understanding how SQLite intends to execute a query. Throws `ArgumentNullException` if either parameter is null; throws `SqliteException` if the SQL is syntactically invalid.

### `public static string ProfileTable(SqliteConnection conn, string tableName, int sampleSize = 1000)`

Samples up to `sampleSize` rows from `tableName` and returns a simple profiling summary: row count estimate, average row size, and a histogram of column value lengths. Throws `ArgumentNullException` if `conn` or `tableName` is null; throws `InvalidOperationException` if `tableName` does not exist.

### `public static string TableStatsOverview(SqliteConnection conn)`

Returns a one-line summary per table: row count, disk size, average row length, and index size. Useful for quick health checks and capacity planning. Throws `ArgumentNullException` if `conn` is null.

### `public static string SuggestIndexes(SqliteConnection conn)`

Analyzes query patterns and existing indexes, then returns a prioritized list of suggested indexes (one per line) that are likely to improve performance. Suggestions include table, columns, and whether a unique index is recommended. Throws `ArgumentNullException` if `conn` is null.

### `public static string MigrationHistory(SqliteConnection conn)`

Reads the `_EFMigrationsHistory` table (if present) and returns a formatted list of applied migrations with their one-way hashes and timestamps. Returns an empty string if the table does not exist. Throws `ArgumentNullException` if `conn` is null.

## Usage

```csharp
// Example 1: Quick schema overview
using var conn = new SqliteConnection("Data Source=chinook.db");
conn.Open();

// List all indexes
Console.WriteLine(SqliteAnalysisTools.ListIndexes(conn));

// Generate an ERD and save to file
var erd = SqliteAnalysisTools.GenerateErd(conn);
File.WriteAllText("erd.dot", erd);
```

```csharp
// Example 2: Diagnose a slow query and suggest indexes
using var conn = new SqliteConnection("Data Source=adventureworks.db");
conn.Open();

var plan = SqliteAnalysisTools.ExplainQueryPlan(conn,
    "SELECT * FROM Sales WHERE CustomerId = 42 AND OrderDate > '2023-01-01'");
Console.WriteLine(plan);

var suggestions = SqliteAnalysisTools.SuggestIndexes(conn);
if (!string.IsNullOrEmpty(suggestions))
    Console.WriteLine("Suggested indexes:\n" + suggestions);
```

## Notes

- All methods are stateless and thread-safe; the `SqliteConnection` must not be closed or disposed while any method is executing.
- Methods that query SQLite metadata (`ListIndexes`, `ListForeignKeys`, etc.) may throw `SqliteException` if the underlying schema is corrupted or inaccessible.
- `ForeignKeyChain` returns an empty string when no path exists; it does not throw if the tables exist but are unconnected.
- `ProfileTable` uses a simple random sample; results are approximate and may vary between runs.
- `SuggestIndexes` bases its recommendations on heuristics and may omit edge cases such as correlated subqueries or CTEs.
