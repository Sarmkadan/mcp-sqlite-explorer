using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace McpSqliteExplorer;

/// <summary>
/// Controls what <see cref="SqliteToolsExtensions.GetSchemaSummary(SqliteExplorer, SchemaSummaryOptions)"/>
/// reports. Bound from the <c>McpSqliteExplorer:SchemaSummary</c> configuration section and validated by
/// <see cref="McpSqliteExplorerOptionsValidator"/> when the host starts.
/// </summary>
public sealed class SchemaSummaryOptions
{
    /// <summary>Largest accepted value for <see cref="SampleRows"/>, matching the query row cap.</summary>
    public const int MaxSampleRows = SqliteExplorer.MaxRowCap;

    /// <summary>Time budget for matching one table name against one pattern.</summary>
    public static readonly TimeSpan PatternMatchTimeout = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Regular expressions a table or view name must match to be selected. Each pattern must match the
    /// whole name, case-insensitively. Empty selects every table and view.
    /// </summary>
    public string[] IncludeTables { get; set; } = [];

    /// <summary>
    /// Regular expressions (whole-name, case-insensitive) that deselect tables and views. A table matched
    /// by both an include and an exclude pattern is rejected at startup.
    /// </summary>
    public string[] ExcludeTables { get; set; } = [];

    /// <summary>When true, the summary reports the row count of each selected table.</summary>
    public bool CountRows { get; set; }

    /// <summary>
    /// Row counts are skipped when more tables and views than this are selected, because each count is a
    /// full scan. Must be zero or greater.
    /// </summary>
    public int MaxCountedTables { get; set; } = 100;

    /// <summary>Rows to sample from each selected table. Zero disables sampling; must not exceed 1000.</summary>
    public int SampleRows { get; set; }

    /// <summary>
    /// Compiles one pattern into a whole-name, case-insensitive matcher.
    /// </summary>
    /// <param name="pattern">The regular expression to compile.</param>
    /// <returns>A compiled matcher bounded by <see cref="PatternMatchTimeout"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> is not a valid regular expression.</exception>
    internal static Regex CompilePattern(string pattern)
    {
        // The raw pattern is checked on its own first, so an unbalanced ')' cannot close the anchoring group.
        _ = new Regex(pattern, RegexOptions.CultureInvariant, PatternMatchTimeout);

        return new Regex(
            @"\A(?:" + pattern + @")\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            PatternMatchTimeout);
    }

    /// <summary>
    /// Decides whether a table or view is part of the summary: it must match an include pattern (when any
    /// are given) and must not match an exclude pattern.
    /// </summary>
    internal static bool IsSelected(string name, IReadOnlyList<Regex> include, IReadOnlyList<Regex> exclude) =>
        (include.Count == 0 || include.Any(pattern => pattern.IsMatch(name)))
        && !exclude.Any(pattern => pattern.IsMatch(name));
}
