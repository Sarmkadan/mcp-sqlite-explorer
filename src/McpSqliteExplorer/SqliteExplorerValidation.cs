using System;
using System.Collections.Generic;
using System.Linq;

namespace McpSqliteExplorer;

/// <summary>
/// Provides validation helpers for <see cref="SqliteExplorer"/> instances and its related record types.
/// </summary>
public static class SqliteExplorerValidation
{
    /// <summary>
    /// Validates the supplied <see cref="SqliteExplorer"/> instance and returns a list of human-readable problems.
    /// </summary>
    /// <remarks>
    /// <see cref="SqliteExplorer"/> instances themselves have no mutable state to validate.
    /// Validation is performed on the records returned by explorer operations.
    /// </remarks>
    /// <param name="value">The explorer instance to validate.</param>
    /// <returns>
    /// An <see cref="IReadOnlyList{T}"/> of problem descriptions.
    /// The list is empty when the instance is considered valid.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    public static IReadOnlyList<string> Validate(this SqliteExplorer value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Array.Empty<string>();
    }

    /// <summary>
    /// Determines whether the supplied <see cref="SqliteExplorer"/> instance is valid.
    /// </summary>
    /// <param name="value">The explorer instance to check.</param>
    /// <returns><c>true</c> if no validation problems are reported; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    public static bool IsValid(this SqliteExplorer value) => value is not null && !value.Validate().Any();

    /// <summary>
    /// Ensures that the supplied <see cref="SqliteExplorer"/> instance is valid.
    /// </summary>
    /// <param name="value">The explorer instance to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when one or more validation problems are detected. The exception message contains a
    /// semicolon-separated list of the problems.
    /// </exception>
    public static void EnsureValid(this SqliteExplorer value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _ = value.Validate(); // Validate call already includes null check
    }

    /// <summary>
    /// Validates the supplied <see cref="TableInfo"/> record and returns a list of human-readable problems.
    /// </summary>
    /// <param name="value">The table info to validate.</param>
    /// <returns>
    /// An <see cref="IReadOnlyList{T}"/> of problem descriptions.
    /// The list is empty when the instance is considered valid.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    public static IReadOnlyList<string> Validate(this TableInfo value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(value.Name))
            problems.Add("TableInfo.Name must not be null or whitespace");

        if (string.IsNullOrWhiteSpace(value.Type))
            problems.Add("TableInfo.Type must not be null or whitespace");
        else if (value.Type.ToUpperInvariant() is not ("TABLE" or "VIEW"))
            problems.Add("TableInfo.Type must be either 'table' or 'view'");

        return problems;
    }

    /// <summary>
    /// Determines whether the supplied <see cref="TableInfo"/> record is valid.
    /// </summary>
    /// <param name="value">The table info to check.</param>
    /// <returns><c>true</c> if no validation problems are reported; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    public static bool IsValid(this TableInfo value) => value.Validate().Count == 0;

    /// <summary>
    /// Ensures that the supplied <see cref="TableInfo"/> record is valid.
    /// </summary>
    /// <param name="value">The table info to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when one or more validation problems are detected. The exception message contains a
    /// semicolon-separated list of the problems.
    /// </exception>
    public static void EnsureValid(this TableInfo value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = value.Validate();
        if (problems.Count > 0)
        {
            var message = $"TableInfo instance is invalid: {string.Join("; ", problems)}";
            throw new ArgumentException(message, nameof(value));
        }
    }

    /// <summary>
    /// Validates the supplied <see cref="ColumnInfo"/> record and returns a list of human-readable problems.
    /// </summary>
    /// <param name="value">The column info to validate.</param>
    /// <returns>
    /// An <see cref="IReadOnlyList{T}"/> of problem descriptions.
    /// The list is empty when the instance is considered valid.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    public static IReadOnlyList<string> Validate(this ColumnInfo value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(value.Name))
            problems.Add("ColumnInfo.Name must not be null or whitespace");

        if (string.IsNullOrWhiteSpace(value.Type))
            problems.Add("ColumnInfo.Type must not be null or whitespace");

        // DefaultValue can be null, but if not null should be non-empty
        if (value.DefaultValue is not null && string.IsNullOrWhiteSpace(value.DefaultValue))
            problems.Add("ColumnInfo.DefaultValue must not be empty if specified");

        return problems;
    }

    /// <summary>
    /// Determines whether the supplied <see cref="ColumnInfo"/> record is valid.
    /// </summary>
    /// <param name="value">The column info to check.</param>
    /// <returns><c>true</c> if no validation problems are reported; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    public static bool IsValid(this ColumnInfo value) => value.Validate().Count == 0;

    /// <summary>
    /// Ensures that the supplied <see cref="ColumnInfo"/> record is valid.
    /// </summary>
    /// <param name="value">The column info to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when one or more validation problems are detected. The exception message contains a
    /// semicolon-separated list of the problems.
    /// </exception>
    public static void EnsureValid(this ColumnInfo value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = value.Validate();
        if (problems.Count > 0)
        {
            var message = $"ColumnInfo instance is invalid: {string.Join("; ", problems)}";
            throw new ArgumentException(message, nameof(value));
        }
    }

    /// <summary>
    /// Validates the supplied <see cref="QueryResult"/> record and returns a list of human-readable problems.
    /// </summary>
    /// <param name="value">The query result to validate.</param>
    /// <returns>
    /// An <see cref="IReadOnlyList{T}"/> of problem descriptions.
    /// The list is empty when the instance is considered valid.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    public static IReadOnlyList<string> Validate(this QueryResult value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = new List<string>();

        if (value.Columns is null)
            problems.Add("QueryResult.Columns must not be null");
        else if (value.Columns.Count == 0)
            problems.Add("QueryResult.Columns must contain at least one column");
        else
        {
            for (var i = 0; i < value.Columns.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(value.Columns[i]))
                    problems.Add($"QueryResult.Columns[{i}] must not be null or whitespace");
            }
        }

        if (value.Rows is null)
            problems.Add("QueryResult.Rows must not be null");
        else
        {
            // Validate each row has the correct number of columns
            foreach (var row in value.Rows)
            {
                if (row is null)
                {
                    problems.Add("QueryResult.Rows must not contain null rows");
                    continue;
                }

                var rowCount = row.Count;
                if (rowCount != value.Columns.Count)
                    problems.Add($"QueryResult.Rows contains a row with {rowCount} columns, expected {value.Columns.Count}");
            }
        }

        if (value.AppliedRowCap <= 0)
            problems.Add("QueryResult.AppliedRowCap must be a positive integer");
        else if (value.AppliedRowCap > SqliteExplorer.MaxRowCap)
            problems.Add($"QueryResult.AppliedRowCap must not exceed {SqliteExplorer.MaxRowCap}");

        return problems;
    }

    /// <summary>
    /// Determines whether the supplied <see cref="QueryResult"/> record is valid.
    /// </summary>
    /// <param name="value">The query result to check.</param>
    /// <returns><c>true</c> if no validation problems are reported; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    public static bool IsValid(this QueryResult value) => value.Validate().Count == 0;

    /// <summary>
    /// Ensures that the supplied <see cref="QueryResult"/> record is valid.
    /// </summary>
    /// <param name="value">The query result to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when one or more validation problems are detected. The exception message contains a
    /// semicolon-separated list of the problems.
    /// </exception>
    public static void EnsureValid(this QueryResult value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = value.Validate();
        if (problems.Count > 0)
        {
            var message = $"QueryResult instance is invalid: {string.Join("; ", problems)}";
            throw new ArgumentException(message, nameof(value));
        }
    }
}