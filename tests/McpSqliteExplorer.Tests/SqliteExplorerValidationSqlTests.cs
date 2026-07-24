using System;
using System.Collections.Generic;
using Xunit;

namespace McpSqliteExplorer.Tests;

/// <summary>
/// Comprehensive unit tests for SQL validation in SqliteExplorerValidation.
/// Tests for valid SELECT statements, multiple statements, write operations,
/// dangerous keywords, whitespace handling, and comment handling.
/// </summary>
public class SqliteExplorerValidationSqlTests
{
    #region Valid SQL Statements

    [Fact]
    public void ValidateSql_WithSimpleSelectStatement_ReturnsEmptyList()
    {
        // Arrange
        var sql = "SELECT * FROM users";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateSql_WithSelectWithWhereClause_ReturnsEmptyList()
    {
        // Arrange
        var sql = "SELECT id, name FROM users WHERE active = 1";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateSql_WithSelectWithOrderBy_ReturnsEmptyList()
    {
        // Arrange
        var sql = "SELECT * FROM users ORDER BY name ASC";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateSql_WithSelectWithLimit_ReturnsEmptyList()
    {
        // Arrange
        var sql = "SELECT * FROM users LIMIT 100";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateSql_WithSelectWithJoins_ReturnsEmptyList()
    {
        // Arrange
        var sql = "SELECT u.name, o.order_id FROM users u JOIN orders o ON u.id = o.user_id";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateSql_WithSelectWithAggregateFunctions_ReturnsEmptyList()
    {
        // Arrange
        var sql = "SELECT COUNT(*), AVG(age), MAX(salary) FROM users";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateSql_WithSelectWithGroupBy_ReturnsEmptyList()
    {
        // Arrange
        var sql = "SELECT department, COUNT(*) FROM employees GROUP BY department";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateSql_WithSelectWithHaving_ReturnsEmptyList()
    {
        // Arrange
        var sql = "SELECT department, COUNT(*) FROM employees GROUP BY department HAVING COUNT(*) > 5";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateSql_WithSelectWithDistinct_ReturnsEmptyList()
    {
        // Arrange
        var sql = "SELECT DISTINCT department FROM employees";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateSql_WithSelectWithSubquery_ReturnsEmptyList()
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE department_id IN (SELECT id FROM departments WHERE active = 1)";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateSql_WithSelectWithAliases_ReturnsEmptyList()
    {
        // Arrange
        var sql = "SELECT u.name AS username, e.salary AS yearly_salary FROM users u JOIN employees e ON u.id = e.user_id";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateSql_WithSelectWithFunctions_ReturnsEmptyList()
    {
        // Arrange
        var sql = "SELECT UPPER(name), LOWER(email), LENGTH(address) FROM users";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateSql_WithSelectWithIsNull_ReturnsEmptyList()
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE middle_name IS NULL";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateSql_WithSelectWithLike_ReturnsEmptyList()
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE name LIKE 'John%'";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateSql_WithSelectWithBetween_ReturnsEmptyList()
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE age BETWEEN 18 AND 65";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region SQL Injection and Multiple Statements

    [Fact]
    public void ValidateSql_WithMultipleStatementsSeparatedBySemicolon_ReturnsError()
    {
        // Arrange
        var sql = "SELECT * FROM users; DELETE FROM users";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert - Multiple statements are detected
        Assert.NotEmpty(result);
        Assert.Contains("multiple statements", result[0]);
    }

    [Fact]
    public void ValidateSql_WithThreeStatementsSeparatedBySemicolons_ReturnsError()
    {
        // Arrange
        var sql = "SELECT 1; SELECT 2; SELECT 3";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Single(result);
        Assert.Equal("SQL statement contains multiple statements separated by semicolon", result[0]);
    }

    [Fact]
    public void ValidateSql_WithInsertStatement_ReturnsError()
    {
        // Arrange
        var sql = "INSERT INTO users (name, email) VALUES ('John', 'john@example.com')";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Single(result);
        Assert.Equal("SQL statement contains write operations (INSERT, UPDATE, DELETE, etc.)", result[0]);
    }

    [Fact]
    public void ValidateSql_WithUpdateStatement_ReturnsError()
    {
        // Arrange
        var sql = "UPDATE users SET email = 'new@example.com' WHERE id = 1";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Single(result);
        Assert.Equal("SQL statement contains write operations (INSERT, UPDATE, DELETE, etc.)", result[0]);
    }

    [Fact]
    public void ValidateSql_WithDeleteStatement_ReturnsError()
    {
        // Arrange
        var sql = "DELETE FROM users WHERE id = 1";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Single(result);
        Assert.Equal("SQL statement contains write operations (INSERT, UPDATE, DELETE, etc.)", result[0]);
    }

    [Fact]
    public void ValidateSql_WithReplaceStatement_ReturnsError()
    {
        // Arrange
        var sql = "REPLACE INTO users (id, name) VALUES (1, 'John')";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Single(result);
        Assert.Equal("SQL statement contains write operations (INSERT, UPDATE, DELETE, etc.)", result[0]);
    }

    [Fact]
    public void ValidateSql_WithUnionStatement_ReturnsError()
    {
        // Arrange
        var sql = "SELECT * FROM users UNION SELECT * FROM admins";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Single(result);
        Assert.Equal("SQL statement contains write operations (INSERT, UPDATE, DELETE, etc.)", result[0]);
    }

    [Fact]
    public void ValidateSql_WithDropTableStatement_ReturnsError()
    {
        // Arrange
        var sql = "DROP TABLE users";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Single(result);
        Assert.Equal("SQL statement contains dangerous keywords (DROP, ALTER, etc.)", result[0]);
    }

    [Fact]
    public void ValidateSql_WithAlterTableStatement_ReturnsError()
    {
        // Arrange
        var sql = "ALTER TABLE users ADD COLUMN new_column TEXT";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Single(result);
        Assert.Equal("SQL statement contains dangerous keywords (DROP, ALTER, etc.)", result[0]);
    }

    [Fact]
    public void ValidateSql_WithCreateTableStatement_ReturnsError()
    {
        // Arrange
        var sql = "CREATE TABLE new_table (id INTEGER PRIMARY KEY, name TEXT)";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Single(result);
        Assert.Equal("SQL statement contains dangerous keywords (DROP, ALTER, etc.)", result[0]);
    }

    [Fact]
    public void ValidateSql_WithCreateIndexStatement_ReturnsError()
    {
        // Arrange
        var sql = "CREATE INDEX idx_name ON users(name)";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Single(result);
        Assert.Equal("SQL statement contains dangerous keywords (DROP, ALTER, etc.)", result[0]);
    }

    [Fact]
    public void ValidateSql_WithTruncateStatement_ReturnsError()
    {
        // Arrange
        var sql = "TRUNCATE TABLE users";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Single(result);
        Assert.Equal("SQL statement contains dangerous keywords (DROP, ALTER, etc.)", result[0]);
    }

    [Fact]
    public void ValidateSql_WithVacuumStatement_ReturnsError()
    {
        // Arrange
        var sql = "VACUUM";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Single(result);
        Assert.Equal("SQL statement contains dangerous keywords (DROP, ALTER, etc.)", result[0]);
    }

    [Fact]
    public void ValidateSql_WithAttachStatement_ReturnsError()
    {
        // Arrange
        var sql = "ATTACH DATABASE 'other.db' AS other";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Single(result);
        Assert.Equal("SQL statement contains dangerous keywords (DROP, ALTER, etc.)", result[0]);
    }

    [Fact]
    public void ValidateSql_WithDetachStatement_ReturnsError()
    {
        // Arrange
        var sql = "DETACH DATABASE 'other'";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Single(result);
        Assert.Equal("SQL statement contains dangerous keywords (DROP, ALTER, etc.)", result[0]);
    }

    #endregion

    #region Null and Empty SQL

    [Fact]
    public void ValidateSql_WithNullSql_ReturnsError()
    {
        // Arrange
        string? sql = null;

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql!);

        // Assert
        Assert.Single(result);
        Assert.Equal("SQL statement must not be empty or whitespace", result[0]);
    }

    [Fact]
    public void ValidateSql_WithEmptySql_ReturnsError()
    {
        // Arrange
        var sql = "";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Single(result);
        Assert.Equal("SQL statement must not be empty or whitespace", result[0]);
    }

    [Fact]
    public void ValidateSql_WithWhitespaceSql_ReturnsError()
    {
        // Arrange
        var sql = "   ";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Single(result);
        Assert.Equal("SQL statement must not be empty or whitespace", result[0]);
    }

    [Fact]
    public void ValidateSql_WithTabWhitespaceSql_ReturnsError()
    {
        // Arrange
        var sql = "\t\t";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Single(result);
        Assert.Equal("SQL statement must not be empty or whitespace", result[0]);
    }

    #endregion

    #region Whitespace Handling

    [Fact]
    public void ValidateSql_WithSqlWithLeadingWhitespace_ReturnsError()
    {
        // Arrange
        var sql = " SELECT * FROM users";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Single(result);
        Assert.Equal("SQL statement contains leading or trailing whitespace", result[0]);
    }

    [Fact]
    public void ValidateSql_WithSqlWithTrailingWhitespace_ReturnsError()
    {
        // Arrange
        var sql = "SELECT * FROM users ";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Single(result);
        Assert.Equal("SQL statement contains leading or trailing whitespace", result[0]);
    }

    [Fact]
    public void ValidateSql_WithSqlWithBothLeadingAndTrailingWhitespace_ReturnsError()
    {
        // Arrange
        var sql = " SELECT * FROM users ";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Single(result);
        Assert.Equal("SQL statement contains leading or trailing whitespace", result[0]);
    }

    #endregion

    #region Comment Handling

    [Fact]
    public void ValidateSql_WithLineCommentContainingWriteKeyword_ReturnsEmptyList()
    {
        // Arrange
        var sql = "SELECT * FROM users -- This is a comment with UPDATE in it";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateSql_WithBlockCommentContainingWriteKeyword_ReturnsEmptyList()
    {
        // Arrange
        var sql = "SELECT * FROM users /* This is a comment with DELETE in it */ WHERE id = 1";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateSql_WithLineCommentContainingDangerousKeyword_ReturnsEmptyList()
    {
        // Arrange
        var sql = "SELECT * FROM users -- Comment with DROP TABLE here";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateSql_WithBlockCommentContainingDangerousKeyword_ReturnsEmptyList()
    {
        // Arrange
        var sql = "SELECT * FROM users /* Comment with ALTER TABLE here */ WHERE id = 1";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateSql_WithNestedBlockComments_ReturnsEmptyList()
    {
        // Arrange
        var sql = "SELECT * FROM users /* Outer comment /* Inner comment */ Outer continues */ WHERE id = 1";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateSql_WithCommentAtStartOfStatement_ReturnsEmptyList()
    {
        // Arrange
        var sql = "-- This is a comment\nSELECT * FROM users";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateSql_WithCommentAtEndOfStatement_ReturnsEmptyList()
    {
        // Arrange
        var sql = "SELECT * FROM users -- End comment";

        // Act
        var result = SqliteExplorerValidation.ValidateSql(sql);

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region Helper Methods Tests

    [Fact]
    public void IsValidSql_WithValidSelectStatement_ReturnsTrue()
    {
        // Arrange
        var sql = "SELECT * FROM users";

        // Act
        var result = SqliteExplorerValidation.IsValidSql(sql);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidSql_WithInvalidSql_ReturnsFalse()
    {
        // Arrange
        var sql = "INSERT INTO users VALUES (1)";

        // Act
        var result = SqliteExplorerValidation.IsValidSql(sql);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidSql_WithNullSql_ReturnsFalse()
    {
        // Arrange
        string? sql = null;

        // Act
        var result = SqliteExplorerValidation.IsValidSql(sql!);

        // Assert - IsValidSql returns false for null instead of throwing
        Assert.False(result);
    }

    [Fact]
    public void EnsureValidSql_WithValidSelectStatement_DoesNotThrow()
    {
        // Arrange
        var sql = "SELECT * FROM users";

        // Act & Assert
        var exception = Record.Exception(() => SqliteExplorerValidation.EnsureValidSql(sql));
        Assert.Null(exception);
    }

    [Fact]
    public void EnsureValidSql_WithInvalidSql_ThrowsArgumentException()
    {
        // Arrange
        var sql = "INSERT INTO users VALUES (1)";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => SqliteExplorerValidation.EnsureValidSql(sql));
        Assert.Contains("SQL statement is invalid", exception.Message);
        Assert.Contains("write operations", exception.Message);
    }

    [Fact]
    public void EnsureValidSql_WithNullSql_ThrowsArgumentNullException()
    {
        // Arrange
        string? sql = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SqliteExplorerValidation.EnsureValidSql(sql!));
    }

    [Fact]
    public void EnsureValidSql_WithEmptySql_ThrowsArgumentException()
    {
        // Arrange
        var sql = "";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => SqliteExplorerValidation.EnsureValidSql(sql));
        Assert.Contains("The value cannot be an empty string", exception.Message);
    }

    #endregion
}