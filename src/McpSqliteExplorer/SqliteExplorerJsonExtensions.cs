using System;
using System.Text.Json;

namespace McpSqliteExplorer;

/// <summary>
/// Provides JSON serialization and deserialization helpers for the <see cref="SqliteExplorer"/> class.
/// </summary>
public static class SqliteExplorerJsonExtensions
{
    /// <summary>
    /// Serializes the specified <see cref="SqliteExplorer"/> instance to a JSON string.
    /// </summary>
    /// <param name="value">The <see cref="SqliteExplorer"/> instance to serialize.</param>
    /// <param name="indented">Whether to format the JSON with indentation.</param>
    /// <returns>The JSON string representation of the <see cref="SqliteExplorer"/> instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static string ToJson(this SqliteExplorer value, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(value);
        var options = SharedJsonExtensions.GetOptions(indented);
        return JsonSerializer.Serialize(value, options);
    }

    /// <summary>
    /// Deserializes a JSON string into a <see cref="SqliteExplorer"/> instance.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized <see cref="SqliteExplorer"/> instance, or <see langword="null"/> if the JSON is invalid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <see langword="null"/>.</exception>
    public static SqliteExplorer? FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            return JsonSerializer.Deserialize<SqliteExplorer>(json, SharedJsonExtensions.DefaultOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Attempts to deserialize a JSON string into a <see cref="SqliteExplorer"/> instance.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="value">The deserialized <see cref="SqliteExplorer"/> instance, or <see langword="null"/> if the JSON is invalid.</param>
    /// <returns><see langword="true"/> if the JSON was successfully deserialized; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <see langword="null"/>.</exception>
    public static bool TryFromJson(string json, out SqliteExplorer? value)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            value = JsonSerializer.Deserialize<SqliteExplorer>(json, SharedJsonExtensions.DefaultOptions);
            return true;
        }
        catch (JsonException)
        {
            value = null;
            return false;
        }
    }
}
