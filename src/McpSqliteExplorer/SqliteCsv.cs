using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace McpSqliteExplorer;

/// <summary>
/// Formats a <see cref="QueryResult"/> as RFC 4180 CSV text: a header row of column names,
/// then one line per row. Fields containing a comma, double quote, CR or LF are quoted with
/// inner quotes doubled; NULL becomes an empty field and BLOBs are base64-encoded, matching the JSON output.
/// </summary>
internal static class SqliteCsv
{
    private const string NewLine = "\r\n";

    /// <summary>
    /// Renders the result as CSV. When the result was truncated or timed out, a final line starting
    /// with <c>#</c> notes it so the reader can tell the output is incomplete.
    /// </summary>
    /// <param name="result">The query result to render.</param>
    /// <returns>The CSV text, terminated by a line break.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is null.</exception>
    public static string Format(QueryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var builder = new StringBuilder();
        AppendLine(builder, result.Columns);

        foreach (var row in result.Rows)
        {
            var fields = new List<string>(row.Count);
            foreach (var value in row)
                fields.Add(FormatValue(value));
            AppendLine(builder, fields);
        }

        if (result.TimedOut)
            builder.Append($"# incomplete: {result.TimeoutMessage} Returned {result.Rows.Count} rows.{NewLine}");
        else if (result.Truncated)
            builder.Append($"# truncated: returned the first {result.Rows.Count} rows (export cap {SqliteExplorer.MaxExportRowCap}); narrow the query or add a WHERE clause to see the rest.{NewLine}");

        return builder.ToString();
    }

    private static void AppendLine(StringBuilder builder, IEnumerable<string> fields)
    {
        var first = true;
        foreach (var field in fields)
        {
            if (!first)
                builder.Append(',');
            first = false;
            builder.Append(Escape(field));
        }
        builder.Append(NewLine);
    }

    private static string FormatValue(object? value) => value switch
    {
        null => string.Empty,
        byte[] bytes => Convert.ToBase64String(bytes),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    private static string Escape(string field)
    {
        if (field.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            return field;

        return "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
