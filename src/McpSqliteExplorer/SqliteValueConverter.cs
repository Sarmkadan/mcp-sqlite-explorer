using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace McpSqliteExplorer;

/// <summary>
/// Provides canonical serialization for SQLite object values (DBNull, byte[], float/double special values).
/// </summary>
public static class SqliteValueConverter
{
    /// <summary>
    /// Maximum size in bytes for inline base64 representation of BLOB values.
    /// Larger blobs are represented as "blob(N bytes)" placeholders.
    /// </summary>
    public const int MaxBlobInlineSize = 1024; // 1KB

    /// <summary>
    /// JSON converter for object? values that handles SQLite-specific types.
    /// </summary>
    public sealed class ObjectConverter : JsonConverter<object?>
    {
        /// <inheritdoc />
        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Delegate to the default behavior for deserialization
            return JsonSerializer.Deserialize<object?>(ref reader, options);
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            // Handle DBNull
            if (value is DBNull)
            {
                writer.WriteNullValue();
                return;
            }

            // Handle byte arrays (BLOBs)
            if (value is byte[] byteArray)
            {
                HandleByteArray(writer, byteArray, options);
                return;
            }

            // Handle floating-point special values (NaN, Infinity)
            if (value is float f)
            {
                HandleFloat(writer, f);
                return;
            }

            if (value is double d)
            {
                HandleDouble(writer, d);
                return;
            }

            // For all other types, use default serialization
            JsonSerializer.Serialize(writer, value, options);
        }

        private static void HandleByteArray(Utf8JsonWriter writer, byte[] byteArray, JsonSerializerOptions options)
        {
            if (byteArray.Length <= MaxBlobInlineSize)
            {
                // Serialize small blobs as base64 strings
                writer.WriteStringValue(Convert.ToBase64String(byteArray));
            }
            else
            {
                // Serialize large blobs as compact placeholders
                writer.WriteStringValue($"blob({byteArray.Length} bytes)");
            }
        }

        private static void HandleFloat(Utf8JsonWriter writer, float value)
        {
            if (float.IsNaN(value))
            {
                writer.WriteStringValue("NaN");
                return;
            }

            if (float.IsPositiveInfinity(value))
            {
                writer.WriteStringValue("Infinity");
                return;
            }

            if (float.IsNegativeInfinity(value))
            {
                writer.WriteStringValue("-Infinity");
                return;
            }

            writer.WriteNumberValue(value);
        }

        private static void HandleDouble(Utf8JsonWriter writer, double value)
        {
            if (double.IsNaN(value))
            {
                writer.WriteStringValue("NaN");
                return;
            }

            if (double.IsPositiveInfinity(value))
            {
                writer.WriteStringValue("Infinity");
                return;
            }

            if (double.IsNegativeInfinity(value))
            {
                writer.WriteStringValue("-Infinity");
                return;
            }

            writer.WriteNumberValue(value);
        }
    }

    /// <summary>
    /// Creates JSON serializer options with the canonical object converter.
    /// </summary>
    /// <param name="camelCaseProperties">Whether to use camelCase property naming.</param>
    /// <param name="writeIndented">Whether to format JSON with indentation.</param>
    /// <param name="includeObjectConverter">Whether to include the ObjectConverter for handling SQLite-specific types.</param>
    /// <returns>Configured JsonSerializerOptions.</returns>
    public static JsonSerializerOptions CreateJsonOptions(
        bool camelCaseProperties = true,
        bool writeIndented = false,
        bool includeObjectConverter = true)
    {
        var options = new JsonSerializerOptions(JsonSerializerOptions.Default)
        {
            WriteIndented = writeIndented,
        };

        if (camelCaseProperties)
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        }

        // Add the canonical object converter only if explicitly requested
        if (includeObjectConverter)
        {
            options.Converters.Add(new ObjectConverter());
        }

        return options;
    }

    /// <summary>
    /// Creates JSON serializer options with the canonical object converter and additional custom converters.
    /// </summary>
    /// <param name="camelCaseProperties">Whether to use camelCase property naming.</param>
    /// <param name="writeIndented">Whether to format JSON with indentation.</param>
    /// <param name="additionalConverters">Additional converters to include.</param>
    /// <returns>Configured JsonSerializerOptions.</returns>
    public static JsonSerializerOptions CreateJsonOptions(
        bool camelCaseProperties,
        bool writeIndented,
        params JsonConverter[] additionalConverters)
    {
        var options = CreateJsonOptions(camelCaseProperties, writeIndented);

        foreach (var converter in additionalConverters)
        {
            options.Converters.Add(converter);
        }

        return options;
    }

    /// <summary>
    /// JSON converter for ValueFrequency records that properly serializes the Value field.
    /// </summary>
    public sealed class ValueFrequencyConverter : JsonConverter<ValueFrequency>
    {
        /// <inheritdoc />
        public override ValueFrequency Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            var value = root.TryGetProperty("value", out var valueProp)
                ? valueProp.Deserialize<object?>(options)
                : null;

            var occurrences = root.TryGetProperty("occurrences", out var occurrencesProp)
                ? occurrencesProp.GetInt64()
                : 0;

            return new ValueFrequency(value, occurrences);
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, ValueFrequency value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("value");
            JsonSerializer.Serialize(writer, value.Value, options);

            writer.WritePropertyName("occurrences");
            writer.WriteNumberValue(value.Occurrences);

            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// JSON converter for QueryResult records that properly serializes Rows containing object values.
    /// </summary>
    public sealed class QueryResultConverter : JsonConverter<QueryResult>
    {
        /// <inheritdoc />
        public override QueryResult Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            var columns = root.TryGetProperty("columns", out var columnsProp)
                ? columnsProp.Deserialize<IReadOnlyList<string>>(options) ?? Array.Empty<string>()
                : Array.Empty<string>();

            var rows = root.TryGetProperty("rows", out var rowsProp)
                ? rowsProp.Deserialize<IReadOnlyList<IReadOnlyList<object?>>>(options) ?? Array.Empty<IReadOnlyList<object?>>()
                : Array.Empty<IReadOnlyList<object?>>();

            var appliedRowCap = root.TryGetProperty("appliedRowCap", out var appliedRowCapProp)
                ? appliedRowCapProp.GetInt32()
                : 0;

            var truncated = root.TryGetProperty("truncated", out var truncatedProp) && truncatedProp.GetBoolean();

            var timedOut = root.TryGetProperty("timedOut", out var timedOutProp) && timedOutProp.GetBoolean();

            var timeoutMessage = root.TryGetProperty("timeoutMessage", out var timeoutMessageProp)
                ? timeoutMessageProp.GetString()
                : null;

            var rowsBeforeTimeout = root.TryGetProperty("rowsBeforeTimeout", out var rowsBeforeTimeoutProp)
                ? rowsBeforeTimeoutProp.GetInt32()
                : 0;

            return new QueryResult(
                columns,
                rows,
                appliedRowCap,
                truncated,
                timedOut,
                timeoutMessage,
                rowsBeforeTimeout
            );
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, QueryResult value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("columns");
            JsonSerializer.Serialize(writer, value.Columns, options);

            writer.WritePropertyName("rows");
            JsonSerializer.Serialize(writer, value.Rows, options);

            writer.WritePropertyName("appliedRowCap");
            writer.WriteNumberValue(value.AppliedRowCap);

            if (value.Truncated)
            {
                writer.WritePropertyName("truncated");
                writer.WriteBooleanValue(true);
            }

            if (value.TimedOut)
            {
                writer.WritePropertyName("timedOut");
                writer.WriteBooleanValue(true);

                if (value.TimeoutMessage != null)
                {
                    writer.WritePropertyName("timeoutMessage");
                    writer.WriteStringValue(value.TimeoutMessage);
                }

                writer.WritePropertyName("rowsBeforeTimeout");
                writer.WriteNumberValue(value.RowsBeforeTimeout);
            }

            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// Serializes a ValueFrequency to JSON.
    /// </summary>
    /// <param name="value">The ValueFrequency to serialize.</param>
    /// <param name="options">JSON serializer options.</param>
    /// <returns>JSON string representation.</returns>
    public static string Serialize(ValueFrequency value, JsonSerializerOptions? options = null)
    {
        var serializerOptions = options ?? CreateJsonOptions(camelCaseProperties: true, writeIndented: false);
        return JsonSerializer.Serialize(value, serializerOptions);
    }

    /// <summary>
    /// Serializes a QueryResult to JSON.
    /// </summary>
    /// <param name="value">The QueryResult to serialize.</param>
    /// <param name="options">JSON serializer options.</param>
    /// <returns>JSON string representation.</returns>
    public static string Serialize(QueryResult value, JsonSerializerOptions? options = null)
    {
        var serializerOptions = options ?? CreateJsonOptions(camelCaseProperties: true, writeIndented: false);
        return JsonSerializer.Serialize(value, serializerOptions);
    }
}