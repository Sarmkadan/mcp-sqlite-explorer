using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace McpSqliteExplorer;

/// <summary>
/// Provides JSON serialization and deserialization helpers for the SqliteExplorer class.
/// </summary>
public static class SqliteExplorerJsonExtensions
{
    /// <summary>
    /// Gets the JSON serialization options used by this class.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>
    /// Serializes the specified SqliteExplorer instance to a JSON string.
    /// </summary>
    /// <param name="value">The SqliteExplorer instance to serialize.</param>
    /// <param name="indented">Whether to format the JSON with indentation.</param>
    /// <returns>The JSON string representation of the SqliteExplorer instance.</returns>
    public static string ToJson(this SqliteExplorer value, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(value);
        return indented ? JsonSerializer.Serialize(value, _jsonOptions) : JsonSerializer.Serialize(value);
    }

    /// <summary>
    /// Deserializes a JSON string into a SqliteExplorer instance.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized SqliteExplorer instance, or null if the JSON is invalid.</returns>
    public static SqliteExplorer? FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            return JsonSerializer.Deserialize<SqliteExplorer>(json, _jsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Attempts to deserialize a JSON string into a SqliteExplorer instance.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="value">The deserialized SqliteExplorer instance, or null if the JSON is invalid.</param>
    /// <returns>True if the JSON was successfully deserialized, false otherwise.</returns>
    public static bool TryFromJson(string json, out SqliteExplorer? value)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            value = JsonSerializer.Deserialize<SqliteExplorer>(json, _jsonOptions);
            return true;
        }
        catch (JsonException)
        {
            value = null;
            return false;
        }
    }
}
