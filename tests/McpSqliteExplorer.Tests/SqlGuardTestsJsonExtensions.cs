using System.Text.Json;
using System.Text.Json.Serialization;
using McpSqliteExplorer.Tests;

/// <summary>
/// Provides JSON serialization and deserialization helpers for <see cref="SqlGuardTests"/>.
/// </summary>
/// <remarks>
/// Uses camelCase property naming policy and indentation for human-readable JSON output.
/// </remarks>
public static class SqlGuardTestsJsonExtensions
{
    /// <summary>
    /// Gets the JSON serializer options used for serialization and deserialization.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Serializes the specified <paramref name="value"/> to a JSON string.
    /// </summary>
    /// <param name="value">The <see cref="SqlGuardTests"/> to serialize.</param>
    /// <param name="indented">Whether to write the JSON with indentation.</param>
    /// <returns>The JSON string representation of the <paramref name="value"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
    public static string ToJson(this SqlGuardTests value, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(value);
        var options = indented ? _jsonSerializerOptions : new JsonSerializerOptions(_jsonSerializerOptions)
        {
            WriteIndented = false
        };
        return JsonSerializer.Serialize(value, options);
    }

    /// <summary>
    /// Deserializes a JSON string into a <see cref="SqlGuardTests"/> instance.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized <see cref="SqlGuardTests"/> instance, or <c>null</c> if the JSON is invalid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <c>null</c>.</exception>
    /// <exception cref="JsonException">The JSON is invalid or cannot be deserialized to <see cref="SqlGuardTests"/>.</exception>
    public static SqlGuardTests? FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize<SqlGuardTests>(json, _jsonSerializerOptions);
    }

    /// <summary>
    /// Attempts to deserialize a JSON string into a <see cref="SqlGuardTests"/> instance.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="value">The deserialized <see cref="SqlGuardTests"/> instance, or <c>null</c> if the JSON is invalid.</param>
    /// <returns><c>true</c> if the JSON was successfully deserialized; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <c>null</c>.</exception>
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
