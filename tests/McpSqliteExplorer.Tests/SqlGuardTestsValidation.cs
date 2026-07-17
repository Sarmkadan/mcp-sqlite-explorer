using System;
using System.Collections.Generic;
using System.Linq;

namespace McpSqliteExplorer.Tests;

/// <summary>
/// Provides validation helpers for <see cref="SqlGuardTests"/> instances.
/// </summary>
public static class SqlGuardTestsValidation
{
    /// <summary>
    /// Validates the supplied <see cref="SqlGuardTests"/> instance and returns a list of human-readable problems.
    /// </summary>
    /// <param name="value">The test class instance to validate.</param>
    /// <returns>
    /// An <see cref="IReadOnlyList{T}"/> of problem descriptions.
    /// The list is empty when the instance is considered valid.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    public static IReadOnlyList<string> Validate(this SqlGuardTests value)
    {
        ArgumentNullException.ThrowIfNull(value);

        // SqlGuardTests is a test class with no state to validate.
        // Returning an empty list signals that the instance is valid.
        return Array.Empty<string>();
    }

    /// <summary>
    /// Determines whether the supplied <see cref="SqlGuardTests"/> instance is valid.
    /// </summary>
    /// <param name="value">The test class instance to check.</param>
    /// <returns><c>true</c> if no validation problems are reported; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    public static bool IsValid(this SqlGuardTests value) =>
        !value.Validate().Any();

    /// <summary>
    /// Ensures that the supplied <see cref="SqlGuardTests"/> instance is valid.
    /// </summary>
    /// <param name="value">The test class instance to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when one or more validation problems are detected. The exception message contains a
    /// semicolon-separated list of the problems.
    /// </exception>
    public static void EnsureValid(this SqlGuardTests value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = value.Validate();
        if (problems.Count > 0)
        {
            var message = $"SqlGuardTests instance is invalid: {string.Join("; ", problems)}";
            throw new ArgumentException(message, nameof(value));
        }
    }
}