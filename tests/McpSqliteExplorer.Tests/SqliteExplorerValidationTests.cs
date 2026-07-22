using System;
using System.Collections.Generic;
using Xunit;

namespace McpSqliteExplorer.Tests;

public class SqliteExplorerValidationTests
{
    [Fact]
    public void SqliteExplorer_Validate_WithValidInstance_ReturnsEmptyList()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.Delete(tempFile);
        File.Create(tempFile).Dispose();
        using var explorer = new SqliteExplorer(tempFile);

        // Act
        var result = explorer.Validate();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void SqliteExplorer_Validate_WithNullInstance_ThrowsArgumentNullException()
    {
        // Arrange
        SqliteExplorer? explorer = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => explorer!.Validate());
    }

    [Fact]
    public void SqliteExplorer_IsValid_WithValidInstance_ReturnsTrue()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.Delete(tempFile);
        File.Create(tempFile).Dispose();
        using var explorer = new SqliteExplorer(tempFile);

        // Act
        var result = explorer.IsValid();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void SqliteExplorer_IsValid_WithNullInstance_ReturnsFalse()
    {
        // Arrange
        SqliteExplorer? explorer = null;

        // Act
        var result = explorer.IsValid();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SqliteExplorer_EnsureValid_WithValidInstance_DoesNotThrow()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.Delete(tempFile);
        File.Create(tempFile).Dispose();
        using var explorer = new SqliteExplorer(tempFile);

        // Act & Assert
        var exception = Record.Exception(() => explorer.EnsureValid());
        Assert.Null(exception);
    }

    [Fact]
    public void SqliteExplorer_EnsureValid_WithNullInstance_ThrowsArgumentNullException()
    {
        // Arrange
        SqliteExplorer? explorer = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => explorer!.EnsureValid());
    }

    [Fact]
    public void TableInfo_Validate_WithValidInstance_ReturnsEmptyList()
    {
        // Arrange
        var tableInfo = new TableInfo("Users", "table");

        // Act
        var result = tableInfo.Validate();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void TableInfo_Validate_WithNullName_ReturnsError()
    {
        // Arrange
        var tableInfo = new TableInfo(null!, "table");

        // Act
        var result = tableInfo.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("TableInfo.Name must not be null or whitespace", result[0]);
    }

    [Fact]
    public void TableInfo_Validate_WithEmptyName_ReturnsError()
    {
        // Arrange
        var tableInfo = new TableInfo(string.Empty, "table");

        // Act
        var result = tableInfo.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("TableInfo.Name must not be null or whitespace", result[0]);
    }

    [Fact]
    public void TableInfo_Validate_WithWhitespaceName_ReturnsError()
    {
        // Arrange
        var tableInfo = new TableInfo("   ", "table");

        // Act
        var result = tableInfo.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("TableInfo.Name must not be null or whitespace", result[0]);
    }

    [Fact]
    public void TableInfo_Validate_WithNullType_ReturnsError()
    {
        // Arrange
        var tableInfo = new TableInfo("Users", null!);

        // Act
        var result = tableInfo.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("TableInfo.Type must not be null or whitespace", result[0]);
    }

    [Fact]
    public void TableInfo_Validate_WithEmptyType_ReturnsError()
    {
        // Arrange
        var tableInfo = new TableInfo("Users", string.Empty);

        // Act
        var result = tableInfo.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("TableInfo.Type must not be null or whitespace", result[0]);
    }

    [Fact]
    public void TableInfo_Validate_WithWhitespaceType_ReturnsError()
    {
        // Arrange
        var tableInfo = new TableInfo("Users", "   ");

        // Act
        var result = tableInfo.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("TableInfo.Type must not be null or whitespace", result[0]);
    }

    [Fact]
    public void TableInfo_Validate_WithInvalidType_ReturnsError()
    {
        // Arrange
        var tableInfo = new TableInfo("Users", "invalid");

        // Act
        var result = tableInfo.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("TableInfo.Type must be either 'table' or 'view'", result[0]);
    }

    [Fact]
    public void TableInfo_Validate_WithViewType_ReturnsEmptyList()
    {
        // Arrange
        var tableInfo = new TableInfo("UsersView", "view");

        // Act
        var result = tableInfo.Validate();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void TableInfo_Validate_WithTableType_ReturnsEmptyList()
    {
        // Arrange
        var tableInfo = new TableInfo("Users", "table");

        // Act
        var result = tableInfo.Validate();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void TableInfo_Validate_WithMixedCaseType_ReturnsEmptyList()
    {
        // Arrange
        var tableInfo = new TableInfo("Users", "TABLE");

        // Act
        var result = tableInfo.Validate();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void TableInfo_IsValid_WithValidInstance_ReturnsTrue()
    {
        // Arrange
        var tableInfo = new TableInfo("Users", "table");

        // Act
        var result = tableInfo.IsValid();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TableInfo_IsValid_WithInvalidInstance_ReturnsFalse()
    {
        // Arrange
        var tableInfo = new TableInfo(null!, "table");

        // Act
        var result = tableInfo.IsValid();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TableInfo_IsValid_WithNullInstance_ThrowsArgumentNullException()
    {
        // Arrange
        TableInfo? tableInfo = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => tableInfo!.IsValid());
    }

    [Fact]
    public void TableInfo_EnsureValid_WithValidInstance_DoesNotThrow()
    {
        // Arrange
        var tableInfo = new TableInfo("Users", "table");

        // Act & Assert
        var exception = Record.Exception(() => tableInfo.EnsureValid());
        Assert.Null(exception);
    }

    [Fact]
    public void TableInfo_EnsureValid_WithInvalidInstance_ThrowsArgumentException()
    {
        // Arrange
        var tableInfo = new TableInfo(null!, "table");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => tableInfo.EnsureValid());
        Assert.Contains("TableInfo instance is invalid", exception.Message);
        Assert.Contains("TableInfo.Name must not be null or whitespace", exception.Message);
    }

    [Fact]
    public void TableInfo_EnsureValid_WithNullInstance_ThrowsArgumentNullException()
    {
        // Arrange
        TableInfo? tableInfo = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => tableInfo!.EnsureValid());
    }

    [Fact]
    public void ColumnInfo_Validate_WithValidInstance_ReturnsEmptyList()
    {
        // Arrange
        var columnInfo = new ColumnInfo("Id", "INTEGER", false, null, true);

        // Act
        var result = columnInfo.Validate();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ColumnInfo_Validate_WithNullName_ReturnsError()
    {
        // Arrange
        var columnInfo = new ColumnInfo(null!, "INTEGER", false, null, true);

        // Act
        var result = columnInfo.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("ColumnInfo.Name must not be null or whitespace", result[0]);
    }

    [Fact]
    public void ColumnInfo_Validate_WithEmptyName_ReturnsError()
    {
        // Arrange
        var columnInfo = new ColumnInfo(string.Empty, "INTEGER", false, null, true);

        // Act
        var result = columnInfo.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("ColumnInfo.Name must not be null or whitespace", result[0]);
    }

    [Fact]
    public void ColumnInfo_Validate_WithWhitespaceName_ReturnsError()
    {
        // Arrange
        var columnInfo = new ColumnInfo("   ", "INTEGER", false, null, true);

        // Act
        var result = columnInfo.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("ColumnInfo.Name must not be null or whitespace", result[0]);
    }

    [Fact]
    public void ColumnInfo_Validate_WithNullType_ReturnsError()
    {
        // Arrange
        var columnInfo = new ColumnInfo("Id", null!, false, null, true);

        // Act
        var result = columnInfo.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("ColumnInfo.Type must not be null or whitespace", result[0]);
    }

    [Fact]
    public void ColumnInfo_Validate_WithEmptyType_ReturnsError()
    {
        // Arrange
        var columnInfo = new ColumnInfo("Id", string.Empty, false, null, true);

        // Act
        var result = columnInfo.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("ColumnInfo.Type must not be null or whitespace", result[0]);
    }

    [Fact]
    public void ColumnInfo_Validate_WithWhitespaceType_ReturnsError()
    {
        // Arrange
        var columnInfo = new ColumnInfo("Id", "   ", false, null, true);

        // Act
        var result = columnInfo.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("ColumnInfo.Type must not be null or whitespace", result[0]);
    }

    [Fact]
    public void ColumnInfo_Validate_WithEmptyDefaultValue_ReturnsError()
    {
        // Arrange
        var columnInfo = new ColumnInfo("Id", "INTEGER", false, string.Empty, true);

        // Act
        var result = columnInfo.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("ColumnInfo.DefaultValue must not be empty if specified", result[0]);
    }

    [Fact]
    public void ColumnInfo_Validate_WithWhitespaceDefaultValue_ReturnsError()
    {
        // Arrange
        var columnInfo = new ColumnInfo("Id", "INTEGER", false, "   ", true);

        // Act
        var result = columnInfo.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("ColumnInfo.DefaultValue must not be empty if specified", result[0]);
    }

    [Fact]
    public void ColumnInfo_Validate_WithNullDefaultValue_ReturnsEmptyList()
    {
        // Arrange
        var columnInfo = new ColumnInfo("Id", "INTEGER", false, null, true);

        // Act
        var result = columnInfo.Validate();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ColumnInfo_IsValid_WithValidInstance_ReturnsTrue()
    {
        // Arrange
        var columnInfo = new ColumnInfo("Id", "INTEGER", false, null, true);

        // Act
        var result = columnInfo.IsValid();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ColumnInfo_IsValid_WithInvalidInstance_ReturnsFalse()
    {
        // Arrange
        var columnInfo = new ColumnInfo(null!, "INTEGER", false, null, true);

        // Act
        var result = columnInfo.IsValid();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ColumnInfo_IsValid_WithNullInstance_ThrowsArgumentNullException()
    {
        // Arrange
        ColumnInfo? columnInfo = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => columnInfo!.IsValid());
    }

    [Fact]
    public void ColumnInfo_EnsureValid_WithValidInstance_DoesNotThrow()
    {
        // Arrange
        var columnInfo = new ColumnInfo("Id", "INTEGER", false, null, true);

        // Act & Assert
        var exception = Record.Exception(() => columnInfo.EnsureValid());
        Assert.Null(exception);
    }

    [Fact]
    public void ColumnInfo_EnsureValid_WithInvalidInstance_ThrowsArgumentException()
    {
        // Arrange
        var columnInfo = new ColumnInfo(null!, "INTEGER", false, null, true);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => columnInfo.EnsureValid());
        Assert.Contains("ColumnInfo instance is invalid", exception.Message);
        Assert.Contains("ColumnInfo.Name must not be null or whitespace", exception.Message);
    }

    [Fact]
    public void ColumnInfo_EnsureValid_WithNullInstance_ThrowsArgumentNullException()
    {
        // Arrange
        ColumnInfo? columnInfo = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => columnInfo!.EnsureValid());
    }

    [Fact]
    public void QueryResult_Validate_WithValidInstance_ReturnsEmptyList()
    {
        // Arrange
        var columns = new List<string> { "Id", "Name", "Email" };
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { 1, "John", "john@example.com" },
            new object?[] { 2, "Jane", "jane@example.com" }
        };
        var queryResult = new QueryResult(columns, rows, 100, false);

        // Act
        var result = queryResult.Validate();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void QueryResult_Validate_WithNullColumns_ReturnsError()
    {
        // Arrange
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { 1, "John", "john@example.com" }
        };
        var queryResult = new QueryResult(null!, rows, 100, false);

        // Act
        var result = queryResult.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("QueryResult.Columns must not be null", result[0]);
    }

    [Fact]
    public void QueryResult_Validate_WithEmptyColumns_ReturnsError()
    {
        // Arrange
        var columns = new List<string>();
        var rows = new List<IReadOnlyList<object?>>();
        var queryResult = new QueryResult(columns, rows, 100, false);

        // Act
        var result = queryResult.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("QueryResult.Columns must contain at least one column", result[0]);
    }

    [Fact]
    public void QueryResult_Validate_WithNullColumnName_ReturnsError()
    {
        // Arrange
        var columns = new List<string> { null!, "Name", "Email" };
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { 1, "John", "john@example.com" }
        };
        var queryResult = new QueryResult(columns, rows, 100, false);

        // Act
        var result = queryResult.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("QueryResult.Columns[0] must not be null or whitespace", result[0]);
    }

    [Fact]
    public void QueryResult_Validate_WithEmptyColumnName_ReturnsError()
    {
        // Arrange
        var columns = new List<string> { string.Empty, "Name", "Email" };
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { 1, "John", "john@example.com" }
        };
        var queryResult = new QueryResult(columns, rows, 100, false);

        // Act
        var result = queryResult.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("QueryResult.Columns[0] must not be null or whitespace", result[0]);
    }

    [Fact]
    public void QueryResult_Validate_WithWhitespaceColumnName_ReturnsError()
    {
        // Arrange
        var columns = new List<string> { "   ", "Name", "Email" };
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { 1, "John", "john@example.com" }
        };
        var queryResult = new QueryResult(columns, rows, 100, false);

        // Act
        var result = queryResult.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("QueryResult.Columns[0] must not be null or whitespace", result[0]);
    }

    [Fact]
    public void QueryResult_Validate_WithNullRows_ReturnsError()
    {
        // Arrange
        var columns = new List<string> { "Id", "Name", "Email" };
        var queryResult = new QueryResult(columns, null!, 100, false);

        // Act
        var result = queryResult.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("QueryResult.Rows must not be null", result[0]);
    }

    [Fact]
    public void QueryResult_Validate_WithNullRowInRows_ReturnsError()
    {
        // Arrange
        var columns = new List<string> { "Id", "Name", "Email" };
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { 1, "John", "john@example.com" },
            null!,
            new object?[] { 2, "Jane", "jane@example.com" }
        };
        var queryResult = new QueryResult(columns, rows, 100, false);

        // Act
        var result = queryResult.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("QueryResult.Rows must not contain null rows", result[0]);
    }

    [Fact]
    public void QueryResult_Validate_WithRowColumnMismatch_ReturnsError()
    {
        // Arrange
        var columns = new List<string> { "Id", "Name", "Email" };
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { 1, "John" }, // Only 2 columns instead of 3
            new object?[] { 2, "Jane", "jane@example.com" }
        };
        var queryResult = new QueryResult(columns, rows, 100, false);

        // Act
        var result = queryResult.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("QueryResult.Rows contains a row with 2 columns, expected 3", result[0]);
    }

    [Fact]
    public void QueryResult_Validate_WithZeroAppliedRowCap_ReturnsError()
    {
        // Arrange
        var columns = new List<string> { "Id", "Name" };
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { 1, "John" }
        };
        var queryResult = new QueryResult(columns, rows, 0, false);

        // Act
        var result = queryResult.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("QueryResult.AppliedRowCap must be a positive integer", result[0]);
    }

    [Fact]
    public void QueryResult_Validate_WithNegativeAppliedRowCap_ReturnsError()
    {
        // Arrange
        var columns = new List<string> { "Id", "Name" };
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { 1, "John" }
        };
        var queryResult = new QueryResult(columns, rows, -1, false);

        // Act
        var result = queryResult.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal("QueryResult.AppliedRowCap must be a positive integer", result[0]);
    }

    [Fact]
    public void QueryResult_Validate_WithMaxRowCapExceeded_ReturnsError()
    {
        // Arrange
        var columns = new List<string> { "Id", "Name" };
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { 1, "John" }
        };
        var queryResult = new QueryResult(columns, rows, SqliteExplorer.MaxRowCap + 1, false);

        // Act
        var result = queryResult.Validate();

        // Assert
        Assert.Single(result);
        Assert.Equal($"QueryResult.AppliedRowCap must not exceed {SqliteExplorer.MaxRowCap}", result[0]);
    }

    [Fact]
    public void QueryResult_IsValid_WithValidInstance_ReturnsTrue()
    {
        // Arrange
        var columns = new List<string> { "Id", "Name" };
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { 1, "John" }
        };
        var queryResult = new QueryResult(columns, rows, 100, false);

        // Act
        var result = queryResult.IsValid();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void QueryResult_IsValid_WithInvalidInstance_ReturnsFalse()
    {
        // Arrange
        var columns = new List<string>();
        var rows = new List<IReadOnlyList<object?>>();
        var queryResult = new QueryResult(columns, rows, 100, false);

        // Act
        var result = queryResult.IsValid();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void QueryResult_IsValid_WithNullInstance_ThrowsArgumentNullException()
    {
        // Arrange
        QueryResult? queryResult = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => queryResult!.IsValid());
    }

    [Fact]
    public void QueryResult_EnsureValid_WithValidInstance_DoesNotThrow()
    {
        // Arrange
        var columns = new List<string> { "Id", "Name" };
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { 1, "John" }
        };
        var queryResult = new QueryResult(columns, rows, 100, false);

        // Act & Assert
        var exception = Record.Exception(() => queryResult.EnsureValid());
        Assert.Null(exception);
    }

    [Fact]
    public void QueryResult_EnsureValid_WithInvalidInstance_ThrowsArgumentException()
    {
        // Arrange
        var columns = new List<string>();
        var rows = new List<IReadOnlyList<object?>>();
        var queryResult = new QueryResult(columns, rows, 100, false);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => queryResult.EnsureValid());
        Assert.StartsWith("QueryResult instance is invalid:", exception.Message);
    }

    [Fact]
    public void QueryResult_EnsureValid_WithNullInstance_ThrowsArgumentNullException()
    {
        // Arrange
        QueryResult? queryResult = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => queryResult!.EnsureValid());
    }
}
