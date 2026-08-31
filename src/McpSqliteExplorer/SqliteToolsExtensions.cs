using System;
using System.Collections.Generic;

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

        return SqliteTools.Guarded(() =>
        {
            var tables = explorer.ListTables();
            return new { count = tables.Count, tables };
        });
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
        ArgumentException.ThrowIfNullOrWhiteSpace(table);

        var sql = $"SELECT COUNT(*) AS row_count FROM {table};";

        return SqliteTools.RunSelect(explorer, sql, 1, 15);
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

        return SqliteTools.Guarded(() =>
        {
            var tables = explorer.ListTables();
            var allColumns = new List<ColumnInfo>();

            foreach (var table in tables)
            {
                foreach (var column in explorer.DescribeTable(table.Name))
                {
                    allColumns.Add(new ColumnInfo(
                        table.Name,
                        table.Type,
                        column.Name,
                        column.Type,
                        column.NotNull,
                        column.PrimaryKey));
                }
            }

            return new { totalColumns = allColumns.Count, columns = allColumns };
        });
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

        return SqliteTools.Guarded(() =>
        {
            var tables = explorer.ListTables();
            var tableCount = 0;
            var viewCount = 0;
            var totalColumns = 0;

            foreach (var table in tables)
            {
                if (string.Equals(table.Type, "table", StringComparison.OrdinalIgnoreCase))
                    tableCount++;
                else if (string.Equals(table.Type, "view", StringComparison.OrdinalIgnoreCase))
                    viewCount++;

                totalColumns += explorer.DescribeTable(table.Name).Count;
            }

            return new
            {
                tables = tableCount,
                views = viewCount,
                totalColumns,
                totalObjects = tables.Count
            };
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
        ArgumentException.ThrowIfNullOrWhiteSpace(table);

        // Cap the requested row count at 100 as per the task description.
        int capped = Math.Min(rowCount, 100);
        if (capped <= 0)
            capped = 1;

        // Build a simple SELECT that orders by RANDOM() and limits the result set.
        // The table name is quoted to avoid issues with reserved words or special characters.
        var sql = $"SELECT * FROM \"{table}\" ORDER BY RANDOM() LIMIT {capped};";

        // Re‑use the existing RunSelect tool which already formats the result as JSON
        // with column names and row data.
        return SqliteTools.RunSelect(explorer, sql, capped, 15);
    }
}
