using System.Data;
using System.Text;
using Microsoft.Data.Sqlite;

namespace McpSqliteExplorer;

/// <summary>
/// Read-only façade over a single SQLite database file. Every method here opens
/// the file in <c>Mode=ReadOnly</c> so nothing this class does can mutate the
/// database, and non-SELECT statements are rejected before they ever reach the
/// engine.
/// </summary>
public sealed class SqliteExplorer
{
    private readonly string _connectionString;

    /// <summary>Hard cap on rows returned by any query, regardless of caller input.</summary>
    public const int MaxRowCap = 1000;

    /// <summary>Default row cap when the caller does not specify one.</summary>
    public const int DefaultRowCap = 100;

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

    private QueryResult ExecuteReadOnly(string sql, int cap)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        var columns = new List<string>();
        var rows = new List<IReadOnlyList<object?>>();
        var truncated = false;

        using var reader = command.ExecuteReader(CommandBehavior.SingleResult);
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
