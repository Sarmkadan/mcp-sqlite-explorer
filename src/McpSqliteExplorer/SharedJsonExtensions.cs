using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace McpSqliteExplorer;

/// <summary>
/// Provides shared JSON serialization configuration for consistent wire format across tool responses.
/// This class consolidates the serialization logic from SqliteExplorerJsonExtensions and SqliteToolsJsonExtensions
/// to ensure all tool responses have a unified JSON format.
/// </summary>
internal static class SharedJsonExtensions
{
    /// <summary>
    /// Creates JSON serializer options with the canonical serialization configuration.
    /// </summary>
    /// <param name="camelCaseProperties">Whether to use camelCase property naming.</param>
    /// <param name="writeIndented">Whether to format JSON with indentation.</param>
    /// <param name="includeObjectConverter">Whether to include the ObjectConverter for handling SQLite-specific types.</param>
    /// <param name="additionalConverters">Additional converters to include.</param>
    /// <returns>Configured JsonSerializerOptions.</returns>
    internal static JsonSerializerOptions CreateJsonOptions(
        bool camelCaseProperties = true,
        bool writeIndented = false,
        bool includeObjectConverter = true,
        params JsonConverter[] additionalConverters)
    {
        var options = SqliteValueConverter.CreateJsonOptions(
            camelCaseProperties: camelCaseProperties,
            writeIndented: writeIndented,
            includeObjectConverter: includeObjectConverter);

        foreach (var converter in additionalConverters)
        {
            options.Converters.Add(converter);
        }

        return options;
    }

    /// <summary>
    /// Default JSON serializer options used for most tool responses.
    /// Uses camelCase property naming, no indentation, and includes the object converter.
    /// </summary>
    internal static JsonSerializerOptions DefaultOptions { get; } = CreateJsonOptions(
        camelCaseProperties: true,
        writeIndented: false,
        includeObjectConverter: false
    );

    /// <summary>
    /// Pretty-printed JSON serializer options used when human-readable output is desired.
    /// Uses camelCase property naming, indentation, and includes the object converter.
    /// </summary>
    internal static JsonSerializerOptions PrettyOptions { get; } = CreateJsonOptions(
        camelCaseProperties: true,
        writeIndented: true,
        includeObjectConverter: false
    );

    /// <summary>
    /// Gets the JSON serializer options for the specified indentation preference.
    /// </summary>
    /// <param name="indented">Whether to format the JSON with indentation.</param>
    /// <returns>Configured JsonSerializerOptions.</returns>
    internal static JsonSerializerOptions GetOptions(bool indented)
    {
        return indented ? PrettyOptions : DefaultOptions;
    }
}
