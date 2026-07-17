using System;
using System.Text.Json;

namespace McpSqliteExplorer.Tests;

/// <summary>
/// Provides JSON serialization and deserialization extensions for <see cref="SqliteExplorerTests"/>.
/// </summary>
public static class SqliteExplorerTestsJsonExtensions
{
    private static readonly JsonSerializerOptions _camelCaseOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Serializes the specified <see cref="SqliteExplorerTests"/> instance to a JSON string.
    /// </summary>
    /// <param name="value">The value to serialize.</param>
    /// <param name="indented">Whether to format the JSON with indentation.</param>
    /// <returns>A JSON string representation of the value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static string ToJson(this SqliteExplorerTests value, bool indented = false)
        => indented
            ? JsonSerializer.Serialize(value, new JsonSerializerOptions(_camelCaseOptions) { WriteIndented = true })
            : JsonSerializer.Serialize(value, _camelCaseOptions);

    /// <summary>
    /// Deserializes a JSON string to a <see cref="SqliteExplorerTests"/> instance.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized instance, or <see langword="null"/> if deserialization fails.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="json"/> is empty or whitespace.</exception>
    public static SqliteExplorerTests? FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        try
        {
            return JsonSerializer.Deserialize<SqliteExplorerTests>(json, _camelCaseOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Attempts to deserialize a JSON string to a <see cref="SqliteExplorerTests"/> instance.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="value">Receives the deserialized instance if successful.</param>
    /// <returns><see langword="true"/> if deserialization succeeds; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="json"/> is empty or whitespace.</exception>
    public static bool TryFromJson(string json, out SqliteExplorerTests? value)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        try
        {
            value = JsonSerializer.Deserialize<SqliteExplorerTests>(json, _camelCaseOptions);
            return true;
        }
        catch (JsonException)
        {
            value = null;
            return false;
        }
    }
}