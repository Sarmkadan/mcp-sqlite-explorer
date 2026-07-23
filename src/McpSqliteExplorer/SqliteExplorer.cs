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
public sealed partial class SqliteExplorer : IDisposable
{
    private readonly string _connectionString;
    private bool _disposed;

    /// <summary>
    /// Finalizer to ensure resources are released if Dispose is not called.
    /// </summary>
    ~SqliteExplorer()
    {
        Dispose(false);
    }

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

    /// <summary>
    /// Disposes the SqliteExplorer and cleans up any resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose, false if called from finalizer.</param>
    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Dispose managed resources
        }

        // No unmanaged resources to dispose in this class
        _disposed = true;
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
    /// <param name="table">Name of the table or view to sample.</param>
    /// <param name="limit">Maximum number of rows to return (1-1000, default 100).</param>
    /// <param name="timeBudgetSeconds">Maximum execution time in seconds (default 15). Set to 0 to disable timeout.</param>
    /// <returns>A <see cref="QueryResult"/> containing the sampled rows and metadata.</returns>
    public QueryResult SampleRows(string table, int limit = DefaultRowCap, int timeBudgetSeconds = 15)
    {
        var safeName = RequireExistingTable(table);
        var cap = ClampLimit(limit);
        var sql = $"SELECT * FROM {QuoteIdentifier(safeName)} LIMIT {cap};";
        return ExecuteReadOnly(sql, cap, timeBudgetSeconds);
    }

    /// <summary>
    /// Runs a single read-only <c>SELECT</c> (or <c>WITH ... SELECT</c>) statement,
    /// capping the number of rows materialised.
    /// </summary>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="limit">Maximum number of rows to return (1-1000, default 100).</param>
    /// <param name="timeBudgetSeconds">Maximum execution time in seconds (default 15). Set to 0 to disable timeout.</param>
    /// <returns>A <see cref="QueryResult"/> containing the query results and metadata.</returns>
    public QueryResult RunSelect(string sql, int limit = DefaultRowCap, int timeBudgetSeconds = 15)
    {
        GuardSelectOnly(sql);
        var cap = ClampLimit(limit);
        return ExecuteReadOnly(sql, cap, timeBudgetSeconds);
    }

    /// <summary>
    /// Returns basic statistics for a table: total row count and, for the first
    /// up‑to‑20 columns, the number of rows where that column is NULL.
    /// </summary>
    /// <param name="table">Name of the table or view to analyse.</param>
    /// <returns>A <see cref="TableProfile"/> containing row count, column profiles for the first 20 columns,
    /// and basic statistics including null counts and rates.</returns>
    /// <exception cref="ArgumentException">Thrown when the table does not exist.</exception>
    public TableProfile TableStats(string table)
    {
        ArgumentException.ThrowIfNullOrEmpty(table);

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
            sb.Append($", SUM(CASE WHEN {QuoteIdentifier(col.Name)} IS NULL THEN 1 ELSE 0 END) AS {QuoteIdentifier(col.Name + "_nulls")}");
        }
        sb.Append($" FROM {QuoteIdentifier(safeName)};");

        var sql = sb.ToString();

        // Execute the query (it is read-only by construction).
        var result = ExecuteReadOnlyCore(sql, DefaultRowCap);

        // The result will contain exactly one row.
        var row = result.Rows[0];
        var rowCount = Convert.ToInt64(Convert.ToInt32(row[0]));

        var profiles = new List<ColumnProfile>();
        for (int i = 0; i < columns.Count; i++)
        {
            var nullCount = Convert.ToInt64(Convert.ToInt32(row[i + 1]));
            var nullRate = rowCount == 0 ? 0.0 : Math.Round((double)nullCount / rowCount, 4);
            profiles.Add(new ColumnProfile(
                Name: columns[i].Name,
                Type: columns[i].Type,
                NullCount: nullCount,
                NullRate: nullRate,
                DistinctCount: 0, // Not computed for basic stats to keep it fast
                Min: null,
                Max: null,
                TopValues: Array.Empty<ValueFrequency>()
            ));
        }

        return new TableProfile(safeName, rowCount, profiles);
    }

    private QueryResult ExecuteReadOnly(string sql, int cap, int timeBudgetSeconds = 15)
    {
        try
        {
            return ExecuteReadOnlyCore(sql, cap, timeBudgetSeconds);
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException(DescribeSqliteError(ex), ex);
        }
    }

    private QueryResult ExecuteReadOnlyCore(string sql, int cap, int timeBudgetSeconds = 15)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        // Apply the configurable timeout (in seconds) to the command.
        // Note: CommandTimeout is in seconds, but we use it as a fallback
        command.CommandTimeout = timeBudgetSeconds > 0 ? timeBudgetSeconds : QueryTimeoutSeconds;

        var columns = new List<string>();
        var rows = new List<IReadOnlyList<object?>>();
        var truncated = false;
        var timedOut = false;
        string? timeoutMessage = null;
        int rowsBeforeTimeout = 0;

        // Create cancellation token with the specified time budget
        // Use linked token source to combine time-based and manual cancellation
        using var cts = timeBudgetSeconds > 0
            ? new CancellationTokenSource(TimeSpan.FromSeconds(timeBudgetSeconds))
            : new CancellationTokenSource();

        // Register command.Cancel() to be called when cancellation is requested
        // This interrupts the SQLite engine mid-execution
        var cancellationTokenRegistration = cts.Token.Register(() =>
        {
            try
            {
                command.Cancel();
            }
            catch
            {
                // Ignore cancellation errors - we're already handling timeout
            }
        });

        try
        {
            // ExecuteReaderAsync respects the cancellation token.
            // Use async/await properly to ensure cancellation works correctly
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

                // Check for cancellation before processing each row
                if (cts.IsCancellationRequested)
                {
                    timedOut = true;
                    rowsBeforeTimeout = rows.Count;
                    break;
                }

                var values = new object?[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                    values[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);

                rows.Add(values);
            }
        }
        // Catch the more specific exception first, then the base.
        catch (TaskCanceledException) when (cts.IsCancellationRequested)
        {
            timedOut = true;
            rowsBeforeTimeout = rows.Count;
            timeoutMessage = $"Query exceeded time budget of {timeBudgetSeconds} seconds.";
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            timedOut = true;
            rowsBeforeTimeout = rows.Count;
            timeoutMessage = $"Query exceeded time budget of {timeBudgetSeconds} seconds.";
        }
        catch (SqliteException ex) when (cts.IsCancellationRequested &&
            (ex.SqliteErrorCode == 9 || ex.Message.Contains("interrupt")))
        {
            // SQLite error 9 is SQLITE_INTERRUPT - query was interrupted
            timedOut = true;
            rowsBeforeTimeout = rows.Count;
            timeoutMessage = $"Query exceeded time budget of {timeBudgetSeconds} seconds.";
        }
        finally
        {
            // Clean up the cancellation registration
            cancellationTokenRegistration.Dispose();
        }

        if (timedOut)
        {
            return new QueryResult(
                Columns: columns,
                Rows: rows,
                AppliedRowCap: cap,
                Truncated: false, // Don't report truncation when timeout occurs
                TimedOut: true,
                TimeoutMessage: timeoutMessage ?? $"Query exceeded time budget of {timeBudgetSeconds} seconds.",
                RowsBeforeTimeout: rowsBeforeTimeout
            );
        }

        return new QueryResult(
            Columns: columns,
            Rows: rows,
            AppliedRowCap: cap,
            Truncated: truncated,
            RowsBeforeTimeout: 0
        );
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
        '"' + identifier.Replace("\"", "\"\"") + '"';
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
    bool Truncated,
    bool TimedOut = false,
    string? TimeoutMessage = null,
    int RowsBeforeTimeout = 0);

[Obsolete("Use TableProfile instead. This type will be removed in a future version.")]
public sealed record ColumnStat(string Name, int NullCount);

[Obsolete("Use TableProfile instead. This type will be removed in a future version.")]
public sealed record TableStatsResult(int RowCount, IReadOnlyList<ColumnStat> ColumnStats);