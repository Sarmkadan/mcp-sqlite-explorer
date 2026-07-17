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

## License

MIT — see [LICENSE](LICENSE).
