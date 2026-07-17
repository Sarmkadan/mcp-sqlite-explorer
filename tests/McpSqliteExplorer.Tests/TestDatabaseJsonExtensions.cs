using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace McpSqliteExplorer.Tests;

/// <summary>
/// Provides JSON serialization and deserialization extensions for <see cref="TestDatabase"/>.
/// </summary>
public static class TestDatabaseJsonExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new SqliteConnectionStringBuilderJsonConverter() }
    };

    /// <summary>
    /// Serializes a <see cref="TestDatabase"/> instance to a JSON string.
    /// </summary>
    /// <param name="value">The test database instance to serialize.</param>
    /// <param name="indented">Whether to format the JSON with indentation for readability.</param>
    /// <returns>A JSON string representation of the test database.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static string ToJson(this TestDatabase value, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(value);

        var options = indented
            ? new JsonSerializerOptions(_jsonOptions) { WriteIndented = true }
            : _jsonOptions;

        return JsonSerializer.Serialize(value, options);
    }

    /// <summary>
    /// Deserializes a JSON string to a <see cref="TestDatabase"/> instance.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized <see cref="TestDatabase"/> instance, or <see langword="null"/> if the JSON is empty or invalid.</returns>
    /// <exception cref="JsonException">Thrown when the JSON is invalid or cannot be deserialized.</exception>
    public static TestDatabase? FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrEmpty(json);

        return JsonSerializer.Deserialize<TestDatabase>(json, _jsonOptions);
    }

    /// <summary>
    /// Attempts to deserialize a JSON string to a <see cref="TestDatabase"/> instance.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="value">Receives the deserialized instance if successful.</param>
    /// <returns><see langword="true"/> if deserialization succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryFromJson(string json, out TestDatabase? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(json);

        try
        {
            value = JsonSerializer.Deserialize<TestDatabase>(json, _jsonOptions);
            return true;
        }
        catch (JsonException)
        {
            value = null;
            return false;
        }
    }

    /// <summary>
    /// Custom JSON converter for <see cref="SqliteConnectionStringBuilder"/> to handle its non-standard property names.
    /// </summary>
    private sealed class SqliteConnectionStringBuilderJsonConverter : JsonConverter<SqliteConnectionStringBuilder>
    {
        public override SqliteConnectionStringBuilder? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException($"Expected string for SqliteConnectionStringBuilder, got {reader.TokenType}");

            var connectionString = reader.GetString() ?? throw new JsonException("Connection string cannot be null");
            return new SqliteConnectionStringBuilder(connectionString);
        }

        public override void Write(
            Utf8JsonWriter writer,
            SqliteConnectionStringBuilder value,
            JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(value);

            writer.WriteStringValue(value.ToString());
        }
    }
}