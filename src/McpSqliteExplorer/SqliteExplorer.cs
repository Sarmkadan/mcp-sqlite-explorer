using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace McpSqliteExplorer;

/// <summary>
/// Read-only façade over a single SQLite database file. Every method here opens
/// the file in <c>Mode=ReadOnly</c> so nothing this class does can mutate the
/// database, and non-SELECT statements are rejected before they ever reach the
/// engine.
/// </summary>
public sealed partial class SqliteExplorer
{
    private readonly string _connectionString;

    /// <summary>Hard cap on rows returned by any query, regardless of caller input.</summary>
    public const int MaxRowCap = 1000;

    /// <summary>Default row cap when the caller does not specify one.</summary>
    public const int DefaultRowCap = 100;

    /// <summary>
    /// Configurable query timeout in seconds. Default is 10 seconds.
    /// </summary>
    public int QueryTimeoutSeconds { get; set; } = 10;

    public SqliteExplorer(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Database path must not be empty.", nameof(databasePath));

        if (!File.Exists(databasePath))
            throw new FileNotFoundException($"SQLite database not found: {databasePath}", databasePath);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            // Read-only handles never take a write lock; this keeps us out of the
            // way of any process actively writing to the file.
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    /// <summary>Lists user tables and views, skipping SQLite's internal bookkeeping tables.</summary>
    public IReadOnlyList<TableInfo> ListTables()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT name, type
            FROM sqlite_master
            WHERE type IN ('table', 'view')
              AND name NOT LIKE 'sqlite_%'
            ORDER BY type, name;
            """;

        var tables = new List<TableInfo>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            tables.Add(new TableInfo(reader.GetString(0), reader.GetString(1)));

        return tables;
    }

    /// <summary>
    /// Returns the column layout for a table or view using <c>PRAGMA table_info</c>.
    /// </summary>
    public IReadOnlyList<ColumnInfo> DescribeTable(string table)
    {
        var safeName = RequireExistingTable(table);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        // PRAGMA does not accept bound parameters for the identifier, so the name
        // is validated against sqlite_master first and quoted here.
        command.CommandText = $"PRAGMA table_info({QuoteIdentifier(safeName)});";

        var columns = new List<ColumnInfo>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(new ColumnInfo(
                Name: reader.GetString(reader.GetOrdinal("name")),
                Type: reader.IsDBNull(reader.GetOrdinal("type")) ? "" : reader.GetString(reader.GetOrdinal("type")),
                NotNull: reader.GetInt64(reader.GetOrdinal("notnull")) != 0,
                DefaultValue: reader.IsDBNull(reader.GetOrdinal("dflt_value")) ? null : reader.GetString(reader.GetOrdinal("dflt_value")),
                PrimaryKey: reader.GetInt64(reader.GetOrdinal("pk")) != 0));
        }

        return columns;
    }

    /// <summary>Returns up to <paramref name="limit"/> sample rows from a table.</summary>
    public QueryResult SampleRows(string table, int limit = DefaultRowCap)
    {
        var safeName = RequireExistingTable(table);
        var cap = ClampLimit(limit);
        var sql = $"SELECT * FROM {QuoteIdentifier(safeName)} LIMIT {cap};";
        return ExecuteReadOnly(sql, cap);
    }

    /// <summary>
    /// Runs a single read-only <c>SELECT</c> (or <c>WITH ... SELECT</c>) statement,
    /// capping the number of rows materialised.
    /// </summary>
    public QueryResult RunSelect(string sql, int limit = DefaultRowCap)
    {
        GuardSelectOnly(sql);
        var cap = ClampLimit(limit);
        return ExecuteReadOnly(sql, cap);
    }

    /// <summary>
    /// Returns basic statistics for a table: total row count and, for the first
    /// up‑to‑20 columns, the number of rows where that column is NULL.
    /// </summary>
    public TableStatsResult TableStats(string table)
    {
        var safeName = RequireExistingTable(table);

        // Get column information and limit to the first 20 columns.
        var allColumns = DescribeTable(table);
        var columns = allColumns.Take(20).ToList();

        // Build a single aggregated query that returns the row count and the NULL
        // count for each of the selected columns.
        var sb = new StringBuilder();
        sb.Append("SELECT COUNT(*) AS rowCount");
        foreach (var col in columns)
        {
            // Use a CASE expression to count NULLs per column.
            sb.Append($", SUM(CASE WHEN {QuoteIdentifier(col.Name)} IS NULL THEN 1 ELSE 0 END) AS {QuoteIdentifier(col.Name)}_nulls");
        }
        sb.Append($" FROM {QuoteIdentifier(safeName)};");

        var sql = sb.ToString();

        // Execute the query (it is read‑only by construction).
        var result = ExecuteReadOnlyCore(sql, DefaultRowCap);

        // The result will contain exactly one row.
        var row = result.Rows[0];
        var rowCount = Convert.ToInt32(row[0]);

        var columnStats = new List<ColumnStat>();
        for (int i = 0; i < columns.Count; i++)
        {
            var nullCount = Convert.ToInt32(row[i + 1]);
            columnStats.Add(new ColumnStat(columns[i].Name, nullCount));
        }

        return new TableStatsResult(rowCount, columnStats);
    }

    private QueryResult ExecuteReadOnly(string sql, int cap)
    {
        try
        {
            return ExecuteReadOnlyCore(sql, cap);
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException(DescribeSqliteError(ex), ex);
        }
    }

    private QueryResult ExecuteReadOnlyCore(string sql, int cap)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        // Apply the configurable timeout (in seconds) to the command.
        command.CommandTimeout = QueryTimeoutSeconds;

        // Use a CancellationTokenSource to enforce the same timeout for async execution.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(QueryTimeoutSeconds));

        var columns = new List<string>();
        var rows = new List<IReadOnlyList<object?>>();
        var truncated = false;

        try
        {
            // ExecuteReaderAsync respects the cancellation token.
            using var reader = command.ExecuteReaderAsync(cts.Token).GetAwaiter().GetResult();

            for (var i = 0; i < reader.FieldCount; i++)
                columns.Add(reader.GetName(i));

            while (reader.Read())
            {
                if (rows.Count >= cap)
                {
                    truncated = true;
                    break;
                }

                var values = new object?[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                    values[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);

                rows.Add(values);
            }
        }
        catch (OperationCanceledException)
        {
            // Translate cancellation into a clear timeout message.
            throw new InvalidOperationException($"Query timed out after {QueryTimeoutSeconds} seconds.");
        }
        catch (TaskCanceledException)
        {
            // Same handling for TaskCanceledException which can surface from async APIs.
            throw new InvalidOperationException($"Query timed out after {QueryTimeoutSeconds} seconds.");
        }

        return new QueryResult(columns, rows, cap, truncated);
    }

    private string RequireExistingTable(string table)
    {
        if (string.IsNullOrWhiteSpace(table))
            throw new ArgumentException("Table name must not be empty.", nameof(table));

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT name FROM sqlite_master
            WHERE type IN ('table', 'view') AND name = $name
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$name", table);

        var resolved = command.ExecuteScalar() as string;
        if (resolved is null)
            throw new ArgumentException($"No such table or view: '{table}'.", nameof(table));

        return resolved;
    }

    internal static int ClampLimit(int limit)
    {
        if (limit <= 0)
            return DefaultRowCap;
        return Math.Min(limit, MaxRowCap);
    }

    /// <summary>
    /// Rejects anything that is not a plain read query. This is defence in depth on
    /// top of the read-only connection: it gives a clear error instead of a SQLite
    /// "attempt to write a readonly database" and blocks multi-statement payloads.
    /// </summary>
    internal static void GuardSelectOnly(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL statement must not be empty.", nameof(sql));

        var stripped = StripComments(sql).Trim();
        if (stripped.Length == 0)
            throw new ArgumentException("SQL statement must not be empty.", nameof(sql));

        // Disallow batching: one statement per call. A trailing semicolon is fine.
        var withoutTrailing = stripped.TrimEnd(';').Trim();
        if (withoutTrailing.Contains(';'))
            throw new ArgumentException("Only a single SQL statement is allowed.", nameof(sql));

        var firstWord = withoutTrailing.Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToUpperInvariant();

        var isRead = firstWord is "SELECT" or "WITH";
        if (!isRead)
            throw new ArgumentException(
                "Only read-only SELECT (or WITH ... SELECT) statements are permitted.", nameof(sql));

        // A CTE could still wrap a writing statement (WITH ... DELETE ...). Reject
        // the write keywords outright when they appear as standalone tokens.
        if (ContainsWriteKeyword(withoutTrailing))
            throw new ArgumentException(
                "Statement contains a write operation, which is not permitted.", nameof(sql));
    }

    private static readonly string[] WriteKeywords =
    [
        "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "CREATE",
        "REPLACE", "TRUNCATE", "ATTACH", "DETACH", "REINDEX", "VACUUM", "PRAGMA",
    ];

    private static bool ContainsWriteKeyword(string sql)
    {
        var tokens = sql.Split(
            [' ', '\t', '\r', '\n', '(', ')', ',', ';'],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            var upper = token.ToUpperInvariant();
            if (Array.IndexOf(WriteKeywords, upper) >= 0)
                return true;
        }

        return false;
    }

    private static string StripComments(string sql)
    {
        var builder = new StringBuilder(sql.Length);
        var i = 0;
        while (i < sql.Length)
        {
            // Line comment
            if (sql[i] == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                while (i < sql.Length && sql[i] != '\n')
                    i++;
                continue;
            }

            // Block comment
            if (sql[i] == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < sql.Length && !(sql[i] == '*' && sql[i + 1] == '/'))
                    i++;
                i += 2;
                continue;
            }

            builder.Append(sql[i]);
            i++;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Translates a raw <see cref="SqliteException"/> into a message that tells the
    /// calling agent what went wrong and, where possible, which tool to use next.
    /// </summary>
    internal static string DescribeSqliteError(SqliteException ex)
    {
        var message = ex.Message;

        if (message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
            return $"{message}. Use list_tables to see the tables and views that exist in this database.";

        if (message.Contains("no such column", StringComparison.OrdinalIgnoreCase))
            return $"{message}. Use describe_table to see the columns of the table you are querying.";

        if (message.Contains("no such function", StringComparison.OrdinalIgnoreCase))
            return $"{message}. Only SQLite's built-in SQL functions are available here.";

        return ex.SqliteErrorCode switch
        {
            5 or 6 => $"{message}. The database is locked by another process; retry once the writer has finished.",
            11 => $"{message}. The database file appears to be corrupted; run 'PRAGMA integrity_check' with the sqlite3 CLI to confirm.",
            14 => $"{message}. The database file could not be opened; check that the path is correct and readable.",
            26 => $"{message}. The file exists but is not a SQLite database (or is encrypted).",
            _ => message,
        };
    }

    private static string QuoteIdentifier(string identifier) =>
        "\"" + identifier.Replace("\"", "\"\"") + "\"";
}

public sealed record TableInfo(string Name, string Type);

public sealed record ColumnInfo(
    string Name,
    string Type,
    bool NotNull,
    string? DefaultValue,
    bool PrimaryKey);

public sealed record QueryResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    int AppliedRowCap,
    bool Truncated);

public sealed record ColumnStat(string Name, int NullCount);

public sealed record TableStatsResult(int RowCount, IReadOnlyList<ColumnStat> ColumnStats);
