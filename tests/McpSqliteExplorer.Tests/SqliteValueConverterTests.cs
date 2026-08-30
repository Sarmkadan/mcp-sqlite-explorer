using System.Text.Json;

namespace McpSqliteExplorer.Tests;

public sealed class SqliteValueConverterTests
{
    [Theory]
    [InlineData(42L, "42")]
    [InlineData(3.5d, "3.5")]
    [InlineData("sqlite", "\"sqlite\"")]
    [InlineData(null, "null")]
    public void ObjectConverter_SerializesTypicalScalarValues(object? value, string expected)
    {
        var options = SqliteValueConverter.CreateJsonOptions();

        var json = JsonSerializer.Serialize(value, options);

        Assert.Equal(expected, json);
    }

    [Fact]
    public void ObjectConverter_SerializesDBNullAsNull()
    {
        var options = SqliteValueConverter.CreateJsonOptions();

        var json = JsonSerializer.Serialize<object?>(DBNull.Value, options);

        Assert.Equal("null", json);
    }

    [Theory]
    [MemberData(nameof(BlobValues))]
    public void ObjectConverter_SerializesBlobs(byte[] value, string expected)
    {
        var options = SqliteValueConverter.CreateJsonOptions();

        var json = JsonSerializer.Serialize<object?>(value, options);

        Assert.Equal(expected, json);
    }

    [Theory]
    [InlineData(double.NaN, "\"NaN\"")]
    [InlineData(double.PositiveInfinity, "\"Infinity\"")]
    [InlineData(double.NegativeInfinity, "\"-Infinity\"")]
    public void ObjectConverter_SerializesNonFiniteDoublesAsStrings(double value, string expected)
    {
        var options = SqliteValueConverter.CreateJsonOptions();

        var json = JsonSerializer.Serialize<object?>(value, options);

        Assert.Equal(expected, json);
    }

    [Fact]
    public void CreateJsonOptions_DefaultOverload_RoundTripsQueryResult()
    {
        var original = CreateQueryResult();
        var options = SqliteValueConverter.CreateJsonOptions(
            camelCaseProperties: true,
            writeIndented: false,
            includeObjectConverter: false);

        var json = JsonSerializer.Serialize(original, options);
        var roundTripped = JsonSerializer.Deserialize<QueryResult>(json, options);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.Columns, roundTripped.Columns);
        Assert.Equal(original.AppliedRowCap, roundTripped.AppliedRowCap);
        Assert.Equal(original.Truncated, roundTripped.Truncated);
        Assert.Equal(original.TimedOut, roundTripped.TimedOut);
        Assert.Equal(original.TimeoutMessage, roundTripped.TimeoutMessage);
        Assert.Equal(original.RowsBeforeTimeout, roundTripped.RowsBeforeTimeout);
        Assert.Equal(7, roundTripped.Rows[0][0].AsJsonElement().GetInt64());
        Assert.Equal("seven", roundTripped.Rows[0][1].AsJsonElement().GetString());
    }

    [Fact]
    public void CreateJsonOptions_AdditionalConvertersOverload_RoundTripsQueryResult()
    {
        var original = CreateQueryResult();
        var options = SqliteValueConverter.CreateJsonOptions(
            camelCaseProperties: true,
            writeIndented: false,
            new SqliteValueConverter.QueryResultConverter());

        var json = JsonSerializer.Serialize(original, options);
        var roundTripped = JsonSerializer.Deserialize<QueryResult>(json, options);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.Columns, roundTripped.Columns);
        Assert.Equal(original.AppliedRowCap, roundTripped.AppliedRowCap);
        Assert.True(roundTripped.Truncated);
        Assert.True(roundTripped.TimedOut);
        Assert.Equal("budget exhausted", roundTripped.TimeoutMessage);
        Assert.Equal(1, roundTripped.RowsBeforeTimeout);
        Assert.Equal(7, roundTripped.Rows[0][0].AsJsonElement().GetInt64());
        Assert.Equal("seven", roundTripped.Rows[0][1].AsJsonElement().GetString());
    }

    [Fact]
    public void Serialize_QueryResult_WithConverter_EmitsExpectedShape()
    {
        var options = SqliteValueConverter.CreateJsonOptions(
            camelCaseProperties: true,
            writeIndented: false,
            new SqliteValueConverter.QueryResultConverter());

        var json = SqliteValueConverter.Serialize(CreateQueryResult(), options);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(new[] { "number", "text" }, root.GetProperty("columns").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(7, root.GetProperty("rows")[0][0].GetInt64());
        Assert.Equal("seven", root.GetProperty("rows")[0][1].GetString());
        Assert.Equal(10, root.GetProperty("appliedRowCap").GetInt32());
        Assert.True(root.GetProperty("truncated").GetBoolean());
        Assert.True(root.GetProperty("timedOut").GetBoolean());
        Assert.Equal("budget exhausted", root.GetProperty("timeoutMessage").GetString());
        Assert.Equal(1, root.GetProperty("rowsBeforeTimeout").GetInt32());
    }

    [Fact]
    public void Serialize_ValueFrequency_WithConverter_EmitsExpectedShape()
    {
        var options = SqliteValueConverter.CreateJsonOptions(
            camelCaseProperties: true,
            writeIndented: false,
            new SqliteValueConverter.ValueFrequencyConverter());

        var json = SqliteValueConverter.Serialize(new ValueFrequency(new byte[] { 0, 1, 2 }, 4), options);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("AAEC", root.GetProperty("value").GetString());
        Assert.Equal(4, root.GetProperty("occurrences").GetInt64());
        Assert.Equal(2, root.EnumerateObject().Count());
    }

    public static TheoryData<byte[], string> BlobValues => new()
    {
        { Array.Empty<byte>(), "\"\"" },
        { new byte[] { 0, 1, 2, 255 }, "\"AAEC/w==\"" },
        { new byte[SqliteValueConverter.MaxBlobInlineSize + 1], "\"blob(1025 bytes)\"" },
    };

    private static QueryResult CreateQueryResult() => new(
        new[] { "number", "text" },
        new IReadOnlyList<object?>[] { new object?[] { 7L, "seven" } },
        AppliedRowCap: 10,
        Truncated: true,
        TimedOut: true,
        TimeoutMessage: "budget exhausted",
        RowsBeforeTimeout: 1);
}

internal static class JsonValueTestExtensions
{
    public static JsonElement AsJsonElement(this object? value) => Assert.IsType<JsonElement>(value);
}
