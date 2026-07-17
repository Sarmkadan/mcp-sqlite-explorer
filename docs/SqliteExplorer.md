# SqliteExplorer

Provides read-only exploration of a SQLite database through a safe, SELECT-only interface. Designed for MCP tooling scenarios where an LLM needs to inspect schema and sample data without risk of mutation.

## API

### `public SqliteExplorer(string connectionString)`

Initializes a new explorer bound to the specified SQLite database.

**Parameters**
- `connectionString` — ADO.NET connection string for the target SQLite database (e.g., `"Data Source=app.db;Mode=ReadOnly"`).

**Throws**
- `ArgumentException` — `connectionString` is null, empty, or whitespace.
- `SqliteException` — The database file cannot be opened (missing, corrupt, or inaccessible).

---

### `public IReadOnlyList<TableInfo> ListTables()`

Returns metadata for all user tables in the database, excluding SQLite system tables (`sqlite_%`).

**Returns**
- Read-only list of `TableInfo` records, one per table. Empty list if the database has no user tables.

**Throws**
- `SqliteException` — A database error occurs while querying the schema.

---

### `public IReadOnlyList<ColumnInfo> DescribeTable(string tableName)`

Returns column metadata for the specified table.

**Parameters**
- `tableName` — Name of the table to inspect (case-insensitive).

**Returns**
- Read-only list of `ColumnInfo` records in ordinal position order. Empty list if the table exists but has no columns (should not occur in valid SQLite databases).

**Throws**
- `ArgumentException` — `tableName` is null, empty, or whitespace.
- `InvalidOperationException` — No table with the given name exists.
- `SqliteException` — A database error occurs while querying the schema.

---

### `public QueryResult SampleRows(string tableName, int limit = 5)`

Returns the first `limit` rows from the specified table using `SELECT * FROM table LIMIT @limit`.

**Parameters**
- `tableName` — Name of the table to sample (case-insensitive).
- `limit` — Maximum number of rows to return. Defaults to 5. Must be > 0.

**Returns**
- `QueryResult` containing column names and up to `limit` rows of data.

**Throws**
- `ArgumentException` — `tableName` is null/empty/whitespace, or `limit` <= 0.
- `InvalidOperationException` — No table with the given name exists.
- `SqliteException` — A database error occurs during query execution.

---

### `public QueryResult RunSelect(string sql, IReadOnlyDictionary<string, object?>? parameters = null)`

Executes a read-only SELECT statement against the database. The SQL is validated to ensure it contains no mutating keywords (INSERT, UPDATE, DELETE, DROP, ALTER, CREATE, REPLACE, PRAGMA with write effects, etc.).

**Parameters**
- `sql` — A single SELECT statement. Multiple statements (separated by `;`) are rejected.
- `parameters` — Optional named parameters for the query (e.g., `new Dictionary<string, object?> { ["$id"] = 42 }`).

**Returns**
- `QueryResult` containing column names and all returned rows.

**Throws**
- `ArgumentException` — `sql` is null, empty, whitespace, or fails the SELECT-only validation.
- `SqliteException` — Syntax error, schema error, or other database execution error.
- `InvalidOperationException` — The SQL contains multiple statements or a non-SELECT statement.

---

### `public sealed record TableInfo(string Name, string? Schema, string Type)`

Represents a table in the database.

**Properties**
- `Name` — Table name.
- `Schema` — Schema name (always `"main"` for attached databases in SQLite; null for temp tables).
- `Type` — Object type (`"table"`, `"view"`, `"index"`, etc.). Only `"table"` and `"view"` are returned by `ListTables`.

---

### `public sealed record ColumnInfo(int Cid, string Name, string Type, bool NotNull, object? DefaultValue, int Pk)`

Represents a column in a table, matching SQLite's `PRAGMA table_info` output.

**Properties**
- `Cid` — Column ordinal (0-based).
- `Name` — Column name.
- `Type` — Declared type affinity (e.g., `"TEXT"`, `"INTEGER"`, `"REAL"`, `"BLOB"`, `"NUMERIC"`).
- `NotNull` — True if the column has a NOT NULL constraint.
- `DefaultValue` — Default value expression (null if none).
- `Pk` — Primary key position (0 = not a PK, 1 = first PK column, etc.).

---

### `public sealed record QueryResult(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows)`

Represents the result of a SELECT query.

**Properties**
- `Columns` — Column names in result-set order.
- `Rows` — Each row is a read-only list of cell values in column order. Null values are represented as `null`. Empty list if the query returned no rows.

## Usage

### Example 1: Schema discovery and sampling

```csharp
using var explorer = new SqliteExplorer("Data Source=chinook.db;Mode=ReadOnly");

var tables = explorer.ListTables();
Console.WriteLine($"Found {tables.Count} tables:");

foreach (var table in tables)
{
    Console.WriteLine($"\n--- {table.Name} ---");
    var columns = explorer.DescribeTable(table.Name);
    foreach (var col in columns)
    {
        Console.WriteLine($"  {col.Name} ({col.Type}){(col.NotNull ? " NOT NULL" : "")}{(col.Pk > 0 ? " PK" : "")}");
    }

    var sample = explorer.SampleRows(table.Name, limit: 3);
    Console.WriteLine($"  Sample ({sample.Rows.Count} rows):");
    foreach (var row in sample.Rows)
    {
        Console.WriteLine($"    [{string.Join(", ", row.Select(v => v?.ToString() ?? "NULL"))}]");
    }
}
```

### Example 2: Parameterized analytical query

```csharp
using var explorer = new SqliteExplorer("Data Source=sales.db;Mode=ReadOnly");

const string sql = @"
    SELECT
        strftime('%Y-%m', OrderDate) AS Month,
        COUNT(*) AS OrderCount,
        SUM(Total) AS Revenue
    FROM Orders
    WHERE OrderDate >= $startDate AND OrderDate < $endDate
    GROUP BY Month
    ORDER BY Month;
";

var parameters = new Dictionary<string, object?>
{
    ["$startDate"] = "2023-01-01",
    ["$endDate"] = "2024-01-01"
};

var result = explorer.RunSelect(sql, parameters);

Console.WriteLine($"{"Month",-10} {"Orders",10} {"Revenue",15}");
foreach (var row in result.Rows)
{
    Console.WriteLine($"{row[0],-10} {row[1],10} {row[2],15:C}");
}
```

## Notes

- **Thread safety**: `SqliteExplorer` is **not thread-safe**. The underlying `SqliteConnection` is opened on construction and held open for the lifetime of the instance. Concurrent calls from multiple threads will cause undefined behavior. Create one instance per thread or synchronize externally.
- **Read-only enforcement**: The connection is opened with `Mode=ReadOnly` when possible, and `RunSelect` performs AST-level validation to reject any statement that could mutate data or schema. However, the validation is heuristic; always use a read-only connection string in production.
- **Connection lifetime**: The connection remains open until the `SqliteExplorer` instance is disposed. For short-lived operations, wrap in a `using` block. For long-running services, dispose during shutdown.
- **Large result sets**: `RunSelect` materializes all rows into memory (`IReadOnlyList<IReadOnlyList<object?>>`). For very large queries, consider paging with `LIMIT`/`OFFSET` in your SQL.
- **Parameter binding**: Parameters in `RunSelect` use SQLite's named parameter syntax (`$name`, `@name`, `:name`). The dictionary keys must include the prefix (e.g., `"$id"`).
- **System tables**: `ListTables` filters out `sqlite_%` tables. `DescribeTable` and `SampleRows` will throw `InvalidOperationException` if passed a system table name.
- **Case sensitivity**: Table and column names are matched case-insensitively per SQLite's default behavior, but the returned metadata preserves the original casing from the schema.
- **Null handling**: `QueryResult.Rows` uses `object?`; database `NULL` maps to C# `null`. `ColumnInfo.DefaultValue` is `null` when no default is defined; otherwise it holds the raw default expression (e.g., `"CURRENT_TIMESTAMP"`, `42`, `'text'`).
