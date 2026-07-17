using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace McpSqliteExplorer;

/// <summary>
/// Provides JSON serialization and deserialization helpers for the <see cref="SqliteExplorer"/> class.
/// </summary>
public static class SqliteExplorerJsonExtensions
{
	/// <summary>
	/// Gets the JSON serialization options used by this class.
	/// </summary>
	private static JsonSerializerOptions JsonOptions { get; } = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true,
	};

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
		return indented
			? JsonSerializer.Serialize(value, JsonOptions)
			: JsonSerializer.Serialize(value, JsonOptions);
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
			return JsonSerializer.Deserialize<SqliteExplorer>(json, JsonOptions);
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
			value = JsonSerializer.Deserialize<SqliteExplorer>(json, JsonOptions);
			return true;
		}
		catch (JsonException)
		{
			value = null;
			return false;
		}
	}
}