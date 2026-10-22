using System.Text.Json;
using McpSqliteExplorer;
using Microsoft.Data.Sqlite;
using Xunit;

namespace McpSqliteExplorer.Tests;

/// <summary>
/// Integration tests for <see cref="SqliteValueConverter.QueryResultConverter"/> against a real SQLite database.
/// These tests seed a table with every SQLite storage class and verify that the converter
/// produces correct JSON output when serializing query results.
/// </summary>
public sealed class QueryResultConverterIntegrationTests : IDisposable
{
    private readonly string _dbPath;

    /// <summary>
    /// SQLite storage class values seeded into the test table.
    /// Each row represents a different storage class with appropriate values.
    /// </summary>
    private const string SeedTableSql = @"
        CREATE TABLE storage_classes (
            id          INTEGER PRIMARY KEY,
            int_val     INTEGER,
            real_val    REAL,
            text_val    TEXT,
            blob_val    BLOB,
            null_val    NULL,
            datetime_val DATETIME,
            boolean_val INTEGER  -- SQLite stores booleans as 0/1 integers
        );

        INSERT INTO storage_classes (id, int_val, real_val, text_val, blob_val, null_val, datetime_val, boolean_val) VALUES
        (1, 42, 3.14, 'hello', X'01020304', NULL, '2024-01-15T10:30:00', 1),
        (2, -999999999999, 2.718281828459045, 'unicode: éèê', X'FF', NULL, '1970-01-01T00:00:00', 0),
        (3, 0, 0.0, '', X'', NULL, NULL, 0);
    ";

    public QueryResultConverterIntegrationTests()
    {
        _dbPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"mcp-sqlite-converter-test-{Guid.NewGuid():N}.db");

        // Create the database and seeded table
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString());
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = SeedTableSql;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void QueryResultConverter_SerializesRealDatabaseQuery_ColumnNamesPreserved()
    {
        // Arrange
        using var explorer = new SqliteExplorer(_dbPath);
        var options = SqliteValueConverter.CreateJsonOptions(
            camelCaseProperties: true,
            writeIndented: false,
            includeObjectConverter: true);

        // Act
        var sql = "SELECT * FROM storage_classes";
        var result = explorer.RunSelect(sql);
        var json = JsonSerializer.Serialize(result, options);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Column names should be preserved
        var columns = root.GetProperty("columns").EnumerateArray()
            .Select(c => c.GetString())
            .ToList();

        Assert.Equal(new[] { "id", "int_val", "real_val", "text_val", "blob_val", "null_val", "datetime_val", "boolean_val" }, columns);
    }

    [Fact]
    public void QueryResultConverter_SerializesRealDatabaseQuery_NullsBecomeNull()
    {
        // Arrange
        using var explorer = new SqliteExplorer(_dbPath);
        var options = SqliteValueConverter.CreateJsonOptions(
            camelCaseProperties: true,
            writeIndented: false,
            includeObjectConverter: true);

        // Act
        var sql = "SELECT * FROM storage_classes";
        var result = explorer.RunSelect(sql);
        var json = JsonSerializer.Serialize(result, options);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Row 1: null_val at index 5 should be JSON null
        var row1 = root.GetProperty("rows")[0];
        var nullVal = row1[5];
        Assert.Equal(JsonValueKind.Null, nullVal.ValueKind);

        // Row 2: null_val at index 5 should be JSON null
        var row2 = root.GetProperty("rows")[1];
        var nullVal2 = row2[5];
        Assert.Equal(JsonValueKind.Null, nullVal2.ValueKind);

        // Row 3: null_val at index 5 should be JSON null
        var row3 = root.GetProperty("rows")[2];
        var nullVal3 = row3[5];
        Assert.Equal(JsonValueKind.Null, nullVal3.ValueKind);
    }

    [Fact]
    public void QueryResultConverter_SerializesRealDatabaseQuery_BlobIsBase64()
    {
        // Arrange
        using var explorer = new SqliteExplorer(_dbPath);
        var options = SqliteValueConverter.CreateJsonOptions(
            camelCaseProperties: true,
            writeIndented: false,
            includeObjectConverter: true);

        // Act
        var sql = "SELECT * FROM storage_classes";
        var result = explorer.RunSelect(sql);
        var json = JsonSerializer.Serialize(result, options);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Row 1: blob_val at index 4 should be base64 "AQIDBA==" (base64 of X'01020304')
        var row1 = root.GetProperty("rows")[0];
        var blobVal = row1[4].GetString();
        Assert.Equal("AQIDBA==", blobVal);

        // Row 2: blob_val at index 4 should be base64 "/w==" (base64 of X'FF')
        var row2 = root.GetProperty("rows")[1];
        var blobVal2 = row2[4].GetString();
        Assert.Equal("/w==", blobVal2);

        // Row 3: blob_val at index 4 should be base64 "" (empty BLOB)
        var row3 = root.GetProperty("rows")[2];
        var blobVal3 = row3[4].GetString();
        Assert.Equal("", blobVal3);
    }

    [Fact]
    public void QueryResultConverter_SerializesRealDatabaseQuery_LargeIntegersPreservePrecision()
    {
        // Arrange
        using var explorer = new SqliteExplorer(_dbPath);
        var options = SqliteValueConverter.CreateJsonOptions(
            camelCaseProperties: true,
            writeIndented: false,
            includeObjectConverter: true);

        // Act
        var sql = "SELECT * FROM storage_classes";
        var result = explorer.RunSelect(sql);
        var json = JsonSerializer.Serialize(result, options);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Row 1: int_val at index 1 should be 42 (not 42.0 or lose precision)
        var row1 = root.GetProperty("rows")[0];
        var intVal = row1[1].GetInt64();
        Assert.Equal(42L, intVal);

        // Row 2: int_val at index 1 should preserve precision
        var row2 = root.GetProperty("rows")[1];
        var intVal2 = row2[1].GetInt64();
        Assert.Equal(-999999999999L, intVal2);
    }

    [Fact]
    public void QueryResultConverter_SerializesRealDatabaseQuery_RealValuesPreserved()
    {
        // Arrange
        using var explorer = new SqliteExplorer(_dbPath);
        var options = SqliteValueConverter.CreateJsonOptions(
            camelCaseProperties: true,
            writeIndented: false,
            includeObjectConverter: true);

        // Act
        var sql = "SELECT * FROM storage_classes";
        var result = explorer.RunSelect(sql);
        var json = JsonSerializer.Serialize(result, options);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Row 1: real_val at index 2 should be 3.14
        var row1 = root.GetProperty("rows")[0];
        var realVal = row1[2].GetDouble();
        Assert.Equal(3.14, realVal, precision: 5);

        // Row 2: real_val at index 2 should be 2.718281828459045
        var row2 = root.GetProperty("rows")[1];
        var realVal2 = row2[2].GetDouble();
        Assert.Equal(2.718281828459045, realVal2, precision: 10);
    }

    [Fact]
    public void QueryResultConverter_SerializesRealDatabaseQuery_TextWithUnicode()
    {
        // Arrange
        using var explorer = new SqliteExplorer(_dbPath);
        var options = SqliteValueConverter.CreateJsonOptions(
            camelCaseProperties: true,
            writeIndented: false,
            includeObjectConverter: true);

        // Act
        var sql = "SELECT * FROM storage_classes";
        var result = explorer.RunSelect(sql);
        var json = JsonSerializer.Serialize(result, options);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Row 1: text_val at index 3 should be "hello"
        var row1 = root.GetProperty("rows")[0];
        var textVal = row1[3].GetString();
        Assert.Equal("hello", textVal);

        // Row 2: text_val at index 3 should contain unicode
        var row2 = root.GetProperty("rows")[1];
        var textVal2 = row2[3].GetString();
        Assert.Contains("unicode", textVal2);
    }

    [Fact]
    public void QueryResultConverter_SerializesRealDatabaseQuery_DatetimeValues()
    {
        // Arrange
        using var explorer = new SqliteExplorer(_dbPath);
        var options = SqliteValueConverter.CreateJsonOptions(
            camelCaseProperties: true,
            writeIndented: false,
            includeObjectConverter: true);

        // Act
        var sql = "SELECT * FROM storage_classes";
        var result = explorer.RunSelect(sql);
        var json = JsonSerializer.Serialize(result, options);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Row 1: datetime_val at index 6 should be '2024-01-15T10:30:00'
        var row1 = root.GetProperty("rows")[0];
        var datetimeVal = row1[6].GetString();
        Assert.Equal("2024-01-15T10:30:00", datetimeVal);

        // Row 2: datetime_val at index 6 should be '1970-01-01T00:00:00'
        var row2 = root.GetProperty("rows")[1];
        var datetimeVal2 = row2[6].GetString();
        Assert.Equal("1970-01-01T00:00:00", datetimeVal2);
    }

    [Fact]
    public void QueryResultConverter_SerializesRealDatabaseQuery_BooleanValues()
    {
        // Arrange
        using var explorer = new SqliteExplorer(_dbPath);
        var options = SqliteValueConverter.CreateJsonOptions(
            camelCaseProperties: true,
            writeIndented: false,
            includeObjectConverter: true);

        // Act
        var sql = "SELECT * FROM storage_classes";
        var result = explorer.RunSelect(sql);
        var json = JsonSerializer.Serialize(result, options);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Row 1: boolean_val at index 7 should be 1 (SQLite stores booleans as integers)
        var row1 = root.GetProperty("rows")[0];
        var boolVal = row1[7].GetInt64();
        Assert.Equal(1L, boolVal);

        // Row 2: boolean_val at index 7 should be 0
        var row2 = root.GetProperty("rows")[1];
        var boolVal2 = row2[7].GetInt64();
        Assert.Equal(0L, boolVal2);

        // Row 3: boolean_val at index 7 should be 0
        var row3 = root.GetProperty("rows")[2];
        var boolVal3 = row3[7].GetInt64();
        Assert.Equal(0L, boolVal3);
    }

    [Fact]
    public void QueryResultConverter_SerializesEmptyResultSet()
    {
        // Arrange
        using var explorer = new SqliteExplorer(_dbPath);
        var options = SqliteValueConverter.CreateJsonOptions(
            camelCaseProperties: true,
            writeIndented: false,
            includeObjectConverter: true);

        // Act - query against a table with no matching rows
        var sql = "SELECT * FROM storage_classes WHERE id = -1";
        var result = explorer.RunSelect(sql);
        var json = JsonSerializer.Serialize(result, options);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Columns should still be present
        var columns = root.GetProperty("columns").EnumerateArray()
            .Select(c => c.GetString())
            .ToList();
        Assert.Equal(new[] { "id", "int_val", "real_val", "text_val", "blob_val", "null_val", "datetime_val", "boolean_val" }, columns);

        // Rows should be empty
        Assert.Empty(root.GetProperty("rows").EnumerateArray());
    }

    [Fact]
    public void QueryResultConverter_SerializesQueryWithDuplicateColumnNames()
    {
        // Arrange
        using var explorer = new SqliteExplorer(_dbPath);
        var options = SqliteValueConverter.CreateJsonOptions(
            camelCaseProperties: true,
            writeIndented: false,
            includeObjectConverter: true);

        // Act - query with duplicate column names (both from same table)
        var sql = "SELECT id AS col1, id AS col2 FROM storage_classes LIMIT 1";
        var result = explorer.RunSelect(sql);
        var json = JsonSerializer.Serialize(result, options);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Columns should contain both names (duplicate names are allowed in JSON)
        var columns = root.GetProperty("columns").EnumerateArray()
            .Select(c => c.GetString())
            .ToList();
        Assert.Equal(2, columns.Count);
        Assert.Contains("col1", columns);
        Assert.Contains("col2", columns);

        // Rows should have 2 values (even though they're the same underlying column)
        var rows = root.GetProperty("rows").EnumerateArray();
        var row = rows.First();
        var rowValues = row.EnumerateArray().ToList();
        Assert.Equal(2, rowValues.Count);
    }
}