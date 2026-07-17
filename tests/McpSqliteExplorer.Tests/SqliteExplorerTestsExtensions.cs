using System.Globalization;
using System.Linq;

namespace McpSqliteExplorer.Tests;

/// <summary>
/// Provides extension methods for <see cref="SqliteExplorerTests"/> to simplify common test scenarios
/// and improve test readability.
/// </summary>
/// <remarks>
/// This static class contains extension methods that provide fluent APIs for common test operations
/// such as creating test databases, extracting values from query results, and asserting table states.
/// </remarks>
public static class SqliteExplorerTestsExtensions
{
    /// <summary>
    /// Creates a test database instance and returns both the explorer and the database path.
    /// </summary>
    /// <param name="tests">The test instance.</param>
    /// <returns>A tuple containing the initialized explorer and database path.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tests"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if database creation fails.</exception>
    public static (SqliteExplorer Explorer, string DbPath) CreateTestDatabase(this SqliteExplorerTests tests)
    {
        ArgumentNullException.ThrowIfNull(tests);

        var db = new TestDatabase();
        return (new SqliteExplorer(db.Path), db.Path);
    }

    /// <summary>
    /// Creates a test database instance and returns the explorer.
    /// </summary>
    /// <param name="tests">The test instance.</param>
    /// <returns>The initialized SqliteExplorer instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tests"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if database creation fails.</exception>
    public static SqliteExplorer CreateExplorer(this SqliteExplorerTests tests)
    {
        ArgumentNullException.ThrowIfNull(tests);

        var db = new TestDatabase();
        return new SqliteExplorer(db.Path);
    }

    /// <summary>
    /// Gets the value of a specific column from a single-row result.
    /// </summary>
    /// <param name="result">The query result.</param>
    /// <param name="columnName">The column name to extract.</param>
    /// <typeparam name="T">The expected type of the column value.</typeparam>
    /// <returns>The column value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="columnName"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Result has no rows or column doesn't exist.</exception>
    public static T GetValue<T>(this SqliteExplorerTests _, QueryResult result, string columnName)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrEmpty(columnName);

        if (result.Rows.Count == 0)
        {
            throw new InvalidOperationException("Result contains no rows.");
        }

        var row = result.Rows[0];
        var columnIndex = FindColumnIndex(result.Columns, columnName);
        if (columnIndex < 0)
        {
            throw new InvalidOperationException($"Column '{columnName}' not found in result.");
        }

        var value = row[columnIndex];
        return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets the value of a specific column from a specific row in the result.
    /// </summary>
    /// <param name="result">The query result.</param>
    /// <param name="rowIndex">The zero-based row index.</param>
    /// <param name="columnName">The column name to extract.</param>
    /// <typeparam name="T">The expected type of the column value.</typeparam>
    /// <returns>The column value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rowIndex"/> is out of range.</exception>
    /// <exception cref="ArgumentException"><paramref name="columnName"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Column doesn't exist in the result.</exception>
    public static T GetValue<T>(
        this SqliteExplorerTests _,
        QueryResult result,
        int rowIndex,
        string columnName)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentOutOfRangeException.ThrowIfNegative(rowIndex);
        ArgumentException.ThrowIfNullOrEmpty(columnName);

        if (rowIndex >= result.Rows.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(rowIndex), rowIndex, "Row index is out of range.");
        }

        var row = result.Rows[rowIndex];
        var columnIndex = FindColumnIndex(result.Columns, columnName);
        if (columnIndex < 0)
        {
            throw new InvalidOperationException($"Column '{columnName}' not found in result.");
        }

        var value = row[columnIndex];
        return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }

    private static int FindColumnIndex(IReadOnlyList<string> columns, string columnName)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            if (string.Equals(columns[i], columnName, StringComparison.Ordinal))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Asserts that a table contains exactly the specified number of rows.
    /// </summary>
    /// <param name="explorer">The SqliteExplorer instance.</param>
    /// <param name="tableName">The table name to check.</param>
    /// <param name="expectedCount">The expected row count.</param>
    /// <exception cref="ArgumentNullException"><paramref name="explorer"/> or <paramref name="tableName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="tableName"/> is null or empty.</exception>
    public static void AssertTableRowCount(
        this SqliteExplorerTests _,
        SqliteExplorer explorer,
        string tableName,
        int expectedCount)
    {
        ArgumentNullException.ThrowIfNull(explorer);
        ArgumentException.ThrowIfNullOrEmpty(tableName);

        var result = explorer.RunSelect($"SELECT COUNT(*) FROM {tableName};");
        var actualCount = Convert.ToInt32(result.Rows[0][0]);

        Assert.Equal(expectedCount, actualCount);
    }

    /// <summary>
    /// Asserts that a table column has a specific value in the first row.
    /// </summary>
    /// <param name="explorer">The SqliteExplorer instance.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="expectedValue">The expected value.</param>
    /// <typeparam name="T">The type of the column value.</typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="explorer"/> or <paramref name="tableName"/> or <paramref name="columnName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="tableName"/> or <paramref name="columnName"/> is null or empty.</exception>
    public static void AssertFirstRowValue<T>(
        this SqliteExplorerTests _,
        SqliteExplorer explorer,
        string tableName,
        string columnName,
        T expectedValue)
    {
        ArgumentNullException.ThrowIfNull(explorer);
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        ArgumentException.ThrowIfNullOrEmpty(columnName);

        var result = explorer.RunSelect($"SELECT {columnName} FROM {tableName} LIMIT 1;");
        var actualValue = GetValue<T>(null, result, 0, columnName);

        Assert.Equal(expectedValue, actualValue);
    }
}