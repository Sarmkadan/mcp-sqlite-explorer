using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace McpSqliteExplorer;

/// <summary>
/// Extension methods for <see cref="SqliteTools"/> that provide additional convenience functionality
/// for working with SQLite databases through the MCP tool interface.
/// </summary>
public static class SqliteToolsExtensions
{
    private sealed record ColumnInfo(string Table, string TableType, string Column, string Type, bool NotNull, bool PrimaryKey);

    /// <summary>
    /// Gets the count of tables and views in the database.
    /// </summary>
    /// <param name="explorer">The SQL explorer instance.</param>
    /// <returns>A JSON string containing the table count.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="explorer"/> is null.</exception>
    public static string GetTableCount(this SqliteExplorer explorer)
    {
        ArgumentNullException.ThrowIfNull(explorer);

        return SqliteTools.ListTables(explorer);
    }

    /// <summary>
    /// Gets the count of rows in a specific table or view.
    /// </summary>
    /// <param name="explorer">The SQL explorer instance.</param>
    /// <param name="table">Name of the table or view to count rows from.</param>
    /// <returns>A JSON string containing the row count.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="explorer"/> or <paramref name="table"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="table"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when SQL execution fails.</exception>
    public static string GetRowCount(this SqliteExplorer explorer, string table)
    {
        ArgumentNullException.ThrowIfNull(explorer);
        ArgumentException.ThrowIfNullOrEmpty(table);

        var sql = $"SELECT COUNT(*) AS row_count FROM {table};";

        return SqliteTools.RunSelect(explorer, sql, 1);
    }

    /// <summary>
    /// Gets the names and types of all columns across all tables in the database.
    /// </summary>
    /// <param name="explorer">The SQL explorer instance.</param>
    /// <returns>A JSON string containing all column information.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="explorer"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when JSON parsing fails or required properties are missing.</exception>
    public static string GetAllColumns(this SqliteExplorer explorer)
    {
        ArgumentNullException.ThrowIfNull(explorer);

        var tablesResult = SqliteTools.ListTables(explorer);
        var tablesPayload = System.Text.Json.JsonSerializer.Deserialize<
            System.Text.Json.JsonElement>(tablesResult);

        var tables = tablesPayload.GetProperty("tables");
        var allColumns = new List<ColumnInfo>();

        foreach (var table in tables.EnumerateArray())
        {
            var tableName = table.GetProperty("name").GetString();
            var tableType = table.GetProperty("type").GetString();

            var columnsResult = SqliteTools.DescribeTable(explorer, tableName);
            var columnsPayload = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(columnsResult);
            var columns = columnsPayload.GetProperty("columns");

            foreach (var column in columns.EnumerateArray())
            {
                allColumns.Add(new ColumnInfo(
                    tableName ?? string.Empty,
                    tableType ?? string.Empty,
                    column.GetProperty("name").GetString() ?? string.Empty,
                    column.GetProperty("type").GetString() ?? string.Empty,
                    column.GetProperty("notNull").GetBoolean(),
                    column.GetProperty("primaryKey").GetBoolean()));
            }
        }

        return System.Text.Json.JsonSerializer.Serialize(new { totalColumns = allColumns.Count, columns = allColumns });
    }

    /// <summary>
    /// Gets a summary of database schema including table counts, view counts, and total column counts.
    /// </summary>
    /// <param name="explorer">The SQL explorer instance.</param>
    /// <returns>A JSON string containing comprehensive schema summary.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="explorer"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when JSON parsing fails or required properties are missing.</exception>
    public static string GetSchemaSummary(this SqliteExplorer explorer)
    {
        ArgumentNullException.ThrowIfNull(explorer);

        var tablesResult = SqliteTools.ListTables(explorer);
        var tablesPayload = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(tablesResult);
        var tables = tablesPayload.GetProperty("tables");

        var tableCount = 0;
        var viewCount = 0;
        var totalColumns = 0;

        foreach (var table in tables.EnumerateArray())
        {
            var tableType = table.GetProperty("type").GetString();
            if (string.Equals(tableType, "table", StringComparison.OrdinalIgnoreCase))
                tableCount++;
            else if (string.Equals(tableType, "view", StringComparison.OrdinalIgnoreCase))
                viewCount++;

            var tableName = table.GetProperty("name").GetString();
            var columnsResult = SqliteTools.DescribeTable(explorer, tableName);
            var columnsPayload = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(columnsResult);
            totalColumns += columnsPayload.GetProperty("columnCount").GetInt32();
        }

        return System.Text.Json.JsonSerializer.Serialize(new
        {
            tables = tableCount,
            views = viewCount,
            totalColumns = totalColumns,
            totalObjects = tables.GetArrayLength()
        });
    }

    /// <summary>
    /// Returns a random sample of rows from the specified table.
    /// </summary>
    /// <param name="explorer">The SQL explorer instance.</param>
    /// <param name="table">The name of the table to sample.</param>
    /// <param name="rowCount">Requested number of rows (capped at 100).</param>
    /// <returns>A JSON string containing the sampled rows and column metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="explorer"/> or <paramref name="table"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="table"/> is empty or whitespace.</exception>
    public static string SampleTable(this SqliteExplorer explorer, string table, int rowCount = 10)
    {
        ArgumentNullException.ThrowIfNull(explorer);
        ArgumentException.ThrowIfNullOrEmpty(table);

        // Cap the requested row count at 100 as per the task description.
        int capped = Math.Min(rowCount, 100);
        if (capped <= 0)
            capped = 1;

        // Build a simple SELECT that orders by RANDOM() and limits the result set.
        // The table name is quoted to avoid issues with reserved words or special characters.
        var sql = $"SELECT * FROM \"{table}\" ORDER BY RANDOM() LIMIT {capped};";

        // Re‑use the existing RunSelect tool which already formats the result as JSON
        // with column names and row data.
        return SqliteTools.RunSelect(explorer, sql, capped);
    }
}
