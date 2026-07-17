using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace McpSqliteExplorer.Tests;

public static class SqliteExplorerTestsJsonExtensions
{
    private static readonly JsonSerializerOptions _camelCaseOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string ToJson(this SqliteExplorerTests value, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(value);
        return indented
            ? JsonSerializer.Serialize(value, _camelCaseOptions)
            : JsonSerializer.Serialize(value, _camelCaseOptions);
    }

    public static SqliteExplorerTests? FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            return JsonSerializer.Deserialize<SqliteExplorerTests>(json, _camelCaseOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool TryFromJson(string json, out SqliteExplorerTests? value)
    {
        ArgumentNullException.ThrowIfNull(json);
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
