using System.Text.Json;
using System.Text.Json.Serialization;
using McpSqliteExplorer.Tests;

/// <summary>
/// Provides JSON serialization and deserialization helpers for <see cref="SqlGuardTests"/>.
/// </summary>
public static class SqlGuardTestsJsonExtensions
{
    /// <summary>
    /// Gets the JSON serializer options used for serialization and deserialization.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Serializes the specified <paramref name="value"/> to a JSON string.
    /// </summary>
    /// <param name="value">The <see cref="SqlGuardTests"/> to serialize.</param>
    /// <param name="indented">Optional; whether to write the JSON with indentation.</param>
    /// <returns>The JSON string representation of the <paramref name="value"/>.</returns>
    public static string ToJson(this SqlGuardTests value, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(value);
        return JsonSerializer.Serialize(value, _jsonSerializerOptions);
    }

    /// <summary>
    /// Deserializes a JSON string into a <see cref="SqlGuardTests"/> instance.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized <see cref="SqlGuardTests"/> instance, or <c>null</c> if the JSON is invalid.</returns>
    public static SqlGuardTests? FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            return JsonSerializer.Deserialize<SqlGuardTests>(json, _jsonSerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Attempts to deserialize a JSON string into a <see cref="SqlGuardTests"/> instance.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="value">The deserialized <see cref="SqlGuardTests"/> instance, or <c>null</c> if the JSON is invalid.</param>
    /// <returns><c>true</c> if the JSON was successfully deserialized; otherwise, <c>false</c>.</returns>
    public static bool TryFromJson(string json, out SqlGuardTests? value)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            value = JsonSerializer.Deserialize<SqlGuardTests>(json, _jsonSerializerOptions);
            return true;
        }
        catch (JsonException)
        {
            value = null;
            return false;
        }
    }
}
