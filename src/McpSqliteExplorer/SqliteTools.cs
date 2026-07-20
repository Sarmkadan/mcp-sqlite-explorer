using System;
using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace McpSqliteExplorer;

/// <summary>
/// MCP tool surface. Each method is a thin adapter around <see cref="SqliteExplorer"/>
/// that shapes the result into JSON an agent can read. All operations are read-only.
/// </summary>
[McpServerToolType]
public sealed class SqliteTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    [McpServerTool(Name = "list_tables")]
    [Description("List the tables and views in the SQLite database (internal sqlite_* objects are hidden).")]
    public static string ListTables(SqliteExplorer explorer) =>
        Guarded(() =>
        {
            var tables = explorer.ListTables();
            return new { count = tables.Count, tables };
        });

    [McpServerTool(Name = "describe_table")]
    [Description("Describe the columns of a table or view: name, declared type, nullability, default value and primary-key flag.")]
    public static string DescribeTable(
        SqliteExplorer explorer,
        [Description("Name of the table or view to describe.")] string table) =>
        Guarded(() =>
        {
            var columns = explorer.DescribeTable(table);
            return new { table, columnCount = columns.Count, columns };
        });

    [McpServerTool(Name = "list_indexes")]
    [Description("List the indexes defined on a table or view, including uniqueness, origin, partial flag and the indexed columns.")]
    public static string ListIndexes(
        SqliteExplorer explorer,
        [Description("Name of the table or view whose indexes should be listed.")] string table) =>
        Guarded(() =>
        {
            var indexes = explorer.ListIndexes(table);
            return new { table, indexCount = indexes.Count, indexes };
        });

    [McpServerTool(Name = "sample_rows")]
    [Description("Return a small sample of rows from a table so the agent can see representative data.")]
    public static string SampleRows(
        SqliteExplorer explorer,
        [Description("Name of the table or view to sample.")] string table,
        [Description("Maximum number of rows to return (1-1000, default 100).")] int limit = SqliteExplorer.DefaultRowCap) =>
        Guarded(() => ToPayload(explorer.SampleRows(table, limit)));

    [McpServerTool(Name = "run_select")]
    [Description("Run a single read-only SELECT (or WITH ... SELECT) query and return the rows, capped for safety. Write statements are rejected.")]
    public static string RunSelect(
        SqliteExplorer explorer,
        [Description("A single SELECT or WITH ... SELECT statement. No INSERT/UPDATE/DELETE/DDL and no multiple statements.")] string sql,
        [Description("Maximum number of rows to return (1-1000, default 100).")] int limit = SqliteExplorer.DefaultRowCap) =>
        Guarded(() => ToPayload(explorer.RunSelect(sql, limit)));

    [McpServerTool(Name = "table_stats")]
    [Description("Return basic statistics for a table: total row count and, for the first up‑to‑20 columns, the number of NULL values per column.")]
    public static string TableStats(
        SqliteExplorer explorer,
        [Description("Name of the table or view to analyse.")] string table) =>
        Guarded(() =>
        {
            var stats = explorer.TableStats(table);
            return new
            {
                table,
                rowCount = stats.RowCount,
                columnStats = stats.ColumnStats
            };
        });

    /// <summary>
    /// Runs a tool body and serialises its result. Validation and lookup failures
    /// (bad table name, rejected write statement) are returned as a structured
    /// <c>{ "error": ... }</c> payload so the calling agent gets an actionable
    /// message instead of an opaque framework error.
    /// </summary>
    internal static string Guarded(Func<object> body)
    {
        try
        {
            return Serialize(body());
        }
        catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or InvalidOperationException)
        {
            return Serialize(new { error = ex.Message });
        }
    }

    private static object ToPayload(QueryResult result)
    {
        var rows = result.Rows
            .Select(row =>
            {
                var dict = new Dictionary<string, object?>(result.Columns.Count);
                for (var i = 0; i < result.Columns.Count; i++)
                    dict[result.Columns[i]] = row[i];
                return dict;
            })
            .ToList();

        return new
        {
            columns = result.Columns,
            rowCount = rows.Count,
            appliedRowCap = result.AppliedRowCap,
            truncated = result.Truncated,
            rows,
        };
    }

    internal static string Serialize(object payload) =>
        JsonSerializer.Serialize(payload, JsonOptions);
}
