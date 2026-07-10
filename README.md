# mcp-sqlite-explorer

A small [Model Context Protocol](https://modelcontextprotocol.io) server that lets an
agent **explore a SQLite database read-only**. Point it at a `.db` file and it exposes
four tools: list the tables, describe a table's schema, peek at sample rows, and run a
capped `SELECT`. Nothing it does can modify the database.

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
  SqliteTools.cs       # MCP tool surface, thin JSON adapters over SqliteExplorer
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

## License

MIT — see [LICENSE](LICENSE).
