using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace McpSqliteExplorer;

public static class SqliteExplorerJsonExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string ToJson(this SqliteExplorer value, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(value);
        return indented ? JsonSerializer.Serialize(value, _jsonOptions) : JsonSerializer.Serialize(value);
    }

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
