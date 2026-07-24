using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace McpSqliteExplorer;

/// <summary>
/// Extension methods for <see cref="QueryResult"/>.
/// </summary>
public static class QueryResultExtensions
{
    /// <summary>
    /// Returns <c>true</c> if the result contains no rows.
    /// </summary>
    public static bool IsEmpty(this QueryResult result) =>
        result?.Rows == null || result.Rows.Count == 0;

    /// <summary>
    /// Formats the result as a Markdown table.
    /// Columns become the header row, rows become the body.
    /// Pipes (<c>|</c>) and line breaks inside cell values are escaped.
    /// </summary>
    public static string ToMarkdownTable(this QueryResult result)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));

        var sb = new StringBuilder();

        // Header
        sb.Append('|');
        foreach (var col in result.Columns)
        {
            sb.Append(EscapeMarkdown(col));
            sb.Append('|');
        }
        sb.AppendLine();

        // Separator
        sb.Append('|');
        foreach (var _ in result.Columns)
        {
            sb.Append("---|");
        }
        sb.AppendLine();

        // Rows
        foreach (var row in result.Rows)
        {
            sb.Append('|');
            foreach (var cell in row)
            {
                sb.Append(EscapeMarkdown(cell?.ToString() ?? string.Empty));
                sb.Append('|');
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats the result as CSV.
    /// Values are escaped according to RFC 4180:
    /// if a value contains a comma, double‑quote, or line break it is wrapped in double quotes,
    /// and any double quote inside the value is doubled.
    /// Null values become empty fields.
    /// </summary>
    public static string ToCsv(this QueryResult result)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));

        var sb = new StringBuilder();

        // Header
        sb.AppendLine(string.Join(",", result.Columns.Select(EscapeCsv)));

        // Rows
        foreach (var row in result.Rows)
        {
            sb.AppendLine(string.Join(",", row.Select(cell => EscapeCsv(cell?.ToString() ?? string.Empty))));
        }

        return sb.ToString();
    }

    // --------------------------------------------------------------------
    // Helper methods
    // --------------------------------------------------------------------
    private static string EscapeMarkdown(string value)
    {
        // Escape pipe characters and replace line breaks with <br> for readability.
        return value
            .Replace("|", "\\|")
            .Replace("\r\n", "<br>")
            .Replace("\n", "<br>")
            .Replace("\r", "<br>");
    }

    private static string EscapeCsv(string value)
    {
        // If the value contains any special CSV characters, wrap it in quotes and double any internal quotes.
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            var escaped = value.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }

        return value;
    }
}
