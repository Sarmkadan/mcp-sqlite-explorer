using System.Data;
using System.Globalization;

namespace McpSqliteExplorer;

/// <summary>
/// Provides useful extension methods for <see cref="SqliteExplorer"/> that simplify
/// common database operations and provide additional convenience APIs.
/// </summary>
public static class SqliteExplorerExtensions
{
    /// <summary>
    /// Gets the first column value from the first row of a query result.
    /// </summary>
    /// <param name="result">The query result.</param>
    /// <typeparam name="T">The expected type of the value.</typeparam>
    /// <returns>The value from the first column of the first row, or <c>default</c> if no rows.</returns>
    /// <exception cref="InvalidCastException">Thrown if the value cannot be cast to <typeparamref name="T"/>.</exception>
    public static T? FirstValue<T>(
        this QueryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Rows.Count == 0
            ? default
            : (T?)result.Rows[0][0];
    }

    /// <summary>
    /// Gets the first column value from the first row of a query result,
    /// converting it to the specified type using invariant culture.
    /// </summary>
    /// <param name="result">The query result.</param>
    /// <typeparam name="T">The expected type of the value.</typeparam>
    /// <returns>The value from the first column of the first row, or <c>default</c> if no rows.</returns>
    /// <exception cref="InvalidCastException">Thrown if the value cannot be cast to <typeparamref name="T"/>.</exception>
    public static T? FirstValueAs<T>(
        this QueryResult result)
        where T : IParsable<T>
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Rows.Count == 0)
            return default;

        var value = result.Rows[0][0];
        return value switch
        {
            null => default,
            T t => t,
            string s => T.Parse(s, CultureInfo.InvariantCulture),
            _ => T.Parse(value.ToString()!, CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// Executes a query and returns the count of rows returned.
    /// </summary>
    /// <param name="explorer">The explorer instance.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="limit">Maximum number of rows to process.</param>
    /// <returns>The number of rows returned by the query.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="explorer"/> or <paramref name="sql"/> is null.</exception>
    public static int CountRows(
        this SqliteExplorer explorer,
        string sql,
        int limit = SqliteExplorer.DefaultRowCap)
    {
        ArgumentNullException.ThrowIfNull(explorer);
        ArgumentNullException.ThrowIfNull(sql);

        var countSql = $"SELECT COUNT(*) FROM ({sql}) LIMIT {SqliteExplorer.ClampLimit(limit)}";
        var result = explorer.RunSelect(countSql, 1);
        var count = result.FirstValue<int?>();
        return count ?? 0;
    }

    /// <summary>
    /// Determines whether a table has any rows.
    /// </summary>
    /// <param name="explorer">The explorer instance.</param>
    /// <param name="table">The table name.</param>
    /// <returns><c>true</c> if the table has at least one row; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="explorer"/> or <paramref name="table"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the table does not exist.</exception>
    public static bool HasRows(
        this SqliteExplorer explorer,
        string table)
    {
        ArgumentNullException.ThrowIfNull(explorer);
        ArgumentNullException.ThrowIfNull(table);

        var result = explorer.SampleRows(table, 1);
        return result.Rows.Count > 0;
    }

    /// <summary>
    /// Gets all table names from the database that match a given pattern.
    /// </summary>
    /// <param name="explorer">The explorer instance.</param>
    /// <param name="pattern">The pattern to match (supports * and ? wildcards).</param>
    /// <returns>A list of table names matching the pattern.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="explorer"/> is null.</exception>
    public static IReadOnlyList<string> GetTablesMatching(
        this SqliteExplorer explorer,
        string pattern)
    {
        ArgumentNullException.ThrowIfNull(explorer);
        ArgumentNullException.ThrowIfNull(pattern);

        var tables = explorer.ListTables();
        var patternLower = pattern.ToLowerInvariant();
        var results = new List<string>();

        foreach (var table in tables)
        {
            var nameLower = table.Name.ToLowerInvariant();
            if (WildcardMatch(nameLower, patternLower))
                results.Add(table.Name);
        }

        return results;
    }

    private static bool WildcardMatch(
        string input,
        string pattern)
    {
        var i = 0;
        var j = 0;
        var inputLen = input.Length;
        var patternLen = pattern.Length;

        while (i < inputLen && j < patternLen)
        {
            if (pattern[j] == '*')
            {
                // Skip consecutive asterisks
                while (j + 1 < patternLen && pattern[j + 1] == '*')
                    j++;

                // Try to match the rest of the pattern
                while (i < inputLen)
                {
                    if (WildcardMatch(input[i..], pattern[(j + 1)..]))
                        return true;
                    i++;
                }
                return j == patternLen;
            }
            else if (pattern[j] == '?' || input[i] == pattern[j])
            {
                i++;
                j++;
            }
            else
            {
                return false;
            }
        }

        return i == inputLen && j == patternLen;
    }
}