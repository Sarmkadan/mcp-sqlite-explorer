using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Options;

namespace McpSqliteExplorer;

/// <summary>
/// Checks <see cref="McpSqliteExplorerOptions"/> and reports every violation at once, so the
/// operator can fix all of them in one pass instead of one restart per problem.
/// </summary>
public sealed class McpSqliteExplorerOptionsValidator : IValidateOptions<McpSqliteExplorerOptions>
{
    /// <summary>Shortest accepted query time budget.</summary>
    public static readonly TimeSpan MinQueryTimeout = TimeSpan.FromSeconds(1);

    /// <summary>Longest accepted query time budget.</summary>
    public static readonly TimeSpan MaxQueryTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Validates the options and collects all failures.
    /// </summary>
    /// <param name="name">Options instance name; ignored, since only the default instance is used.</param>
    /// <param name="options">The bound options to check.</param>
    /// <returns>Success, or a failure listing each violated rule.</returns>
    public ValidateOptionsResult Validate(string? name, McpSqliteExplorerOptions options)
    {
        var section = McpSqliteExplorerOptions.SectionName;
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.DatabasePath))
        {
            failures.Add($"{section}:DatabasePath must not be empty (pass the path as the first argument or set SQLITE_DB_PATH).");
        }
        else if (!options.AllowMissingDatabase && !File.Exists(options.DatabasePath.Trim()))
        {
            failures.Add($"{section}:DatabasePath '{options.DatabasePath}' does not exist as a file " +
                $"(set {section}:AllowMissingDatabase=true to allow a missing file).");
        }

        if (options.MaxRows <= 0)
        {
            failures.Add($"{section}:MaxRows must be greater than 0 (got {options.MaxRows}).");
        }

        if (options.QueryTimeout < MinQueryTimeout || options.QueryTimeout > MaxQueryTimeout)
        {
            failures.Add($"{section}:QueryTimeout must be between {MinQueryTimeout} and {MaxQueryTimeout} " +
                $"(got {options.QueryTimeout}).");
        }

        if (options.CatalogCacheTtl < TimeSpan.Zero)
        {
            failures.Add($"{section}:CatalogCacheTtl must not be negative (got {options.CatalogCacheTtl}).");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
