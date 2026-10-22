using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
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

        ValidateSchemaSummary(options, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>
    /// Checks the <c>SchemaSummary</c> subsection: the patterns, the counting limit and the sample size.
    /// </summary>
    private static void ValidateSchemaSummary(McpSqliteExplorerOptions options, List<string> failures)
    {
        var summary = options.SchemaSummary;
        var section = $"{McpSqliteExplorerOptions.SectionName}:SchemaSummary";

        var include = CompilePatterns(section, nameof(summary.IncludeTables), summary.IncludeTables, failures);
        var exclude = CompilePatterns(section, nameof(summary.ExcludeTables), summary.ExcludeTables, failures);

        if (include is not null && exclude is not null)
        {
            ValidateNoTableIsIncludedAndExcluded(section, options, include, exclude, failures);
        }

        if (summary.MaxCountedTables < 0)
        {
            failures.Add($"{section}:MaxCountedTables must not be negative (got {summary.MaxCountedTables}).");
        }

        if (summary.SampleRows < 0 || summary.SampleRows > SchemaSummaryOptions.MaxSampleRows)
        {
            failures.Add($"{section}:SampleRows must be between 0 and {SchemaSummaryOptions.MaxSampleRows} " +
                $"(got {summary.SampleRows}).");
        }
    }

    /// <summary>
    /// Compiles every pattern in one list. Returns null when any pattern is blank or invalid, so the
    /// caller can skip checks that depend on the full list.
    /// </summary>
    private static IReadOnlyList<Regex>? CompilePatterns(
        string section, string property, IReadOnlyList<string> patterns, List<string> failures)
    {
        var compiled = new List<Regex>(patterns.Count);
        var allValid = true;

        for (var i = 0; i < patterns.Count; i++)
        {
            var pattern = patterns[i];
            if (string.IsNullOrWhiteSpace(pattern))
            {
                failures.Add($"{section}:{property}[{i}] must not be blank.");
                allValid = false;
                continue;
            }

            try
            {
                compiled.Add(SchemaSummaryOptions.CompilePattern(pattern));
            }
            catch (ArgumentException ex)
            {
                failures.Add($"{section}:{property}[{i}] '{pattern}' is not a valid regular expression: {ex.Message}");
                allValid = false;
            }
        }

        return allValid ? compiled : null;
    }

    /// <summary>
    /// Rejects a table that is both selected by an include pattern and removed by an exclude pattern.
    /// This needs the real table names, so it reads them from the database file. It is skipped when the
    /// file is missing or cannot be read; the database check reports those problems itself.
    /// </summary>
    private static void ValidateNoTableIsIncludedAndExcluded(
        string section,
        McpSqliteExplorerOptions options,
        IReadOnlyList<Regex> include,
        IReadOnlyList<Regex> exclude,
        List<string> failures)
    {
        // With no include patterns every table is selected, so exclude patterns cannot conflict with it.
        if (include.Count == 0 || exclude.Count == 0 || !TryReadTableNames(options.DatabasePath, out var names))
        {
            return;
        }

        var conflicts = names
            .Where(name => include.Any(pattern => pattern.IsMatch(name))
                && exclude.Any(pattern => pattern.IsMatch(name)))
            .ToList();

        if (conflicts.Count > 0)
        {
            failures.Add($"{section}: table(s) matched by both IncludeTables and ExcludeTables: " +
                string.Join(", ", conflicts.Select(name => $"'{name}'")) + ".");
        }
    }

    /// <summary>
    /// Reads the names of the tables and views that <see cref="SqliteExplorer.ListTables"/> would return,
    /// without the explorer's path restrictions. Returns false when the file is missing or unreadable.
    /// </summary>
    private static bool TryReadTableNames(string databasePath, out IReadOnlyList<string> names)
    {
        names = [];

        if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath.Trim()))
        {
            return false;
        }

        try
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath.Trim(),
                Mode = SqliteOpenMode.ReadOnly,
            }.ToString());
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT name FROM sqlite_master
                WHERE type IN ('table', 'view') AND name NOT LIKE 'sqlite_%';
                """;

            var found = new List<string>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                found.Add(reader.GetString(0));
            }

            names = found;
            return true;
        }
        catch (SqliteException)
        {
            return false;
        }
    }
}
