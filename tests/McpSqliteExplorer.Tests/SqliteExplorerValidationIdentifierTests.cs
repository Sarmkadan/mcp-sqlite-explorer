using System;
using System.Collections.Generic;
using Xunit;

namespace McpSqliteExplorer.Tests;

/// <summary>
/// Comprehensive unit tests for identifier validation in SqliteExplorerValidation.
/// Tests for valid simple identifiers, SQL injection payloads, malformed identifiers,
/// empty/null inputs, length limits, reserved keywords, unicode/non-ASCII identifiers,
/// and whitespace handling.
/// </summary>
public class SqliteExplorerValidationIdentifierTests
{
    #region Valid Simple Identifiers

    [Fact]
    public void ValidateIdentifier_WithSimpleLowercaseIdentifier_ReturnsEmptyList()
    {
        // Arrange
        var identifier = "users";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateIdentifier_WithSimpleUppercaseIdentifier_ReturnsEmptyList()
    {
        // Arrange
        var identifier = "USERS";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateIdentifier_WithMixedCaseIdentifier_ReturnsEmptyList()
    {
        // Arrange
        var identifier = "UserAccounts";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierWithUnderscores_ReturnsEmptyList()
    {
        // Arrange
        var identifier = "user_accounts";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierWithNumbers_ReturnsEmptyList()
    {
        // Arrange
        var identifier = "users2";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierStartingWithNumber_ReturnsEmptyList()
    {
        // Arrange
        var identifier = "123test";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateIdentifier_WithValidIdentifierWithDashes_ReturnsEmptyList()
    {
        // Arrange
        var identifier = "user-accounts";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region SQL Injection Attempts

    [Fact]
    public void ValidateIdentifier_WithIdentifierContainingSelectInMiddle_ReturnsError()
    {
        // Arrange
        var identifier = "usersSELECTfrom";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains SQL injection attempt", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierContainingInsertInMiddle_ReturnsError()
    {
        // Arrange
        var identifier = "usersINSERTusers";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains SQL injection attempt", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierContainingUpdateInMiddle_ReturnsError()
    {
        // Arrange
        var identifier = "usersUPDATEusers";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains SQL injection attempt", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierContainingDeleteInMiddle_ReturnsError()
    {
        // Arrange
        var identifier = "usersDELETEusers";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains SQL injection attempt", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierContainingDropInMiddle_ReturnsError()
    {
        // Arrange
        var identifier = "usersDROPusers";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains SQL injection attempt", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierContainingUnion_ReturnsError()
    {
        // Arrange
        var identifier = "usersUNIONall";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains SQL injection attempt", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierContainingOrKeyword_ReturnsError()
    {
        // Arrange
        var identifier = "users OR 1=1";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains SQL injection attempt", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierContainingAndKeyword_ReturnsError()
    {
        // Arrange
        var identifier = "users AND active=1";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains SQL injection attempt", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierContainingCommentDashDash_ReturnsError()
    {
        // Arrange
        var identifier = "users--comment";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains SQL injection attempt", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierContainingCommentSlashStar_ReturnsError()
    {
        // Arrange
        var identifier = "users/*comment*/";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains SQL injection attempt", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierContainingSemicolon_ReturnsError()
    {
        // Arrange
        var identifier = "users;DROP TABLE users";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains SQL injection attempt", result[0]);
    }

    #endregion

    #region Malformed Identifiers

    [Fact]
    public void ValidateIdentifier_WithIdentifierContainingDoubleQuote_ReturnsError()
    {
        // Arrange
        var identifier = "users\"";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains invalid characters or improper escaping", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierContainingSingleQuote_ReturnsEmptyList()
    {
        // Arrange
        var identifier = "users'name";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert - SQLite allows single quotes in identifiers
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierContainingSquareBrackets_ReturnsError()
    {
        // Arrange
        var identifier = "[users]";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains invalid characters or improper escaping", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierContainingBackticks_ReturnsError()
    {
        // Arrange
        var identifier = "`users`";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains invalid characters or improper escaping", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierContainingParentheses_ReturnsEmptyList()
    {
        // Arrange
        var identifier = "users(name)";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert - SQLite allows parentheses in identifiers
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierContainingControlCharacters_ReturnsError()
    {
        // Arrange
        var identifier = "users\x00test";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains invalid characters or improper escaping", result[0]);
    }

    #endregion

    #region Null and Empty Inputs

    [Fact]
    public void ValidateIdentifier_WithNullIdentifier_ReturnsError()
    {
        // Arrange
        string? identifier = null;

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier!);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier must not be empty or whitespace", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithEmptyIdentifier_ReturnsError()
    {
        // Arrange
        var identifier = "";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier must not be empty or whitespace", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithWhitespaceIdentifier_ReturnsError()
    {
        // Arrange
        var identifier = " ";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier must not be empty or whitespace", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithTabWhitespaceIdentifier_ReturnsError()
    {
        // Arrange
        var identifier = "\t";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier must not be empty or whitespace", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithNewlineWhitespaceIdentifier_ReturnsError()
    {
        // Arrange
        var identifier = "\n";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier must not be empty or whitespace", result[0]);
    }

    #endregion

    #region Length Limits

    [Fact]
    public void ValidateIdentifier_WithIdentifierExceedingMaxLength_ReturnsError()
    {
        // Arrange
        var identifier = new string('a', 256); // 256 characters, exceeds 255 limit

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Contains("exceeds maximum length of 255", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierAtMaxLength_ReturnsEmptyList()
    {
        // Arrange
        var identifier = new string('a', 255); // Exactly 255 characters

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierOneBelowMaxLength_ReturnsEmptyList()
    {
        // Arrange
        var identifier = new string('a', 254); // 254 characters

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region Reserved Keywords

    [Fact]
    public void ValidateIdentifier_WithReservedKeywordSELECT_ReturnsError()
    {
        // Arrange
        var identifier = "SELECT";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier is a SQLite reserved keyword (use quoted identifier if intentional)", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithReservedKeywordFROM_ReturnsError()
    {
        // Arrange
        var identifier = "FROM";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier is a SQLite reserved keyword (use quoted identifier if intentional)", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithReservedKeywordWHERE_ReturnsError()
    {
        // Arrange
        var identifier = "WHERE";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier is a SQLite reserved keyword (use quoted identifier if intentional)", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithReservedKeywordGROUP_ReturnsError()
    {
        // Arrange
        var identifier = "GROUP";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier is a SQLite reserved keyword (use quoted identifier if intentional)", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithReservedKeywordORDER_ReturnsError()
    {
        // Arrange
        var identifier = "ORDER";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier is a SQLite reserved keyword (use quoted identifier if intentional)", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithReservedKeywordJOIN_ReturnsError()
    {
        // Arrange
        var identifier = "JOIN";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier is a SQLite reserved keyword (use quoted identifier if intentional)", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithReservedKeywordCaseInsensitive_ReturnsError()
    {
        // Arrange
        var identifier = "select";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier is a SQLite reserved keyword (use quoted identifier if intentional)", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithReservedKeywordMixedCase_ReturnsError()
    {
        // Arrange
        var identifier = "SeLeCt";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier is a SQLite reserved keyword (use quoted identifier if intentional)", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithReservedKeywordAllowed_ReturnsEmptyList()
    {
        // Arrange
        var identifier = "SELECT";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier, allowReservedKeywords: true);

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region Unicode and Non-ASCII Characters

    [Fact]
    public void ValidateIdentifier_WithNonAsciiCharacters_ReturnsError()
    {
        // Arrange
        var identifier = "users_日本語";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains non-ASCII characters (may cause compatibility issues)", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithCyrillicCharacters_ReturnsError()
    {
        // Arrange
        var identifier = "пользователи";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains non-ASCII characters (may cause compatibility issues)", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithChineseCharacters_ReturnsError()
    {
        // Arrange
        var identifier = "用户表";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains non-ASCII characters (may cause compatibility issues)", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithEmoji_ReturnsError()
    {
        // Arrange
        var identifier = "users😀";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains non-ASCII characters (may cause compatibility issues)", result[0]);
    }

    #endregion

    #region Whitespace Handling

    [Fact]
    public void ValidateIdentifier_WithIdentifierWithLeadingWhitespace_ReturnsError()
    {
        // Arrange
        var identifier = " users";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains leading or trailing whitespace", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierWithTrailingWhitespace_ReturnsError()
    {
        // Arrange
        var identifier = "users ";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains leading or trailing whitespace", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierWithBothLeadingAndTrailingWhitespace_ReturnsError()
    {
        // Arrange
        var identifier = " users ";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains leading or trailing whitespace", result[0]);
    }

    [Fact]
    public void ValidateIdentifier_WithIdentifierWithMultipleSpaces_ReturnsError()
    {
        // Arrange
        var identifier = "  users  ";

        // Act
        var result = SqliteExplorerValidation.ValidateIdentifier(identifier);

        // Assert
        Assert.Single(result);
        Assert.Equal("Identifier contains leading or trailing whitespace", result[0]);
    }

    #endregion

    #region Helper Methods Tests

    [Fact]
    public void IsValidIdentifier_WithValidIdentifier_ReturnsTrue()
    {
        // Arrange
        var identifier = "users";

        // Act
        var result = SqliteExplorerValidation.IsValidIdentifier(identifier);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidIdentifier_WithInvalidIdentifier_ReturnsFalse()
    {
        // Arrange
        var identifier = "users; DROP TABLE users";

        // Act
        var result = SqliteExplorerValidation.IsValidIdentifier(identifier);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidIdentifier_WithNullIdentifier_ReturnsFalse()
    {
        // Arrange
        string? identifier = null;

        // Act
        var result = SqliteExplorerValidation.IsValidIdentifier(identifier!);

        // Assert - IsValidIdentifier returns false for null instead of throwing
        Assert.False(result);
    }

    [Fact]
    public void EnsureValidIdentifier_WithValidIdentifier_DoesNotThrow()
    {
        // Arrange
        var identifier = "users";

        // Act & Assert
        var exception = Record.Exception(() => SqliteExplorerValidation.EnsureValidIdentifier(identifier));
        Assert.Null(exception);
    }

    [Fact]
    public void EnsureValidIdentifier_WithInvalidIdentifier_ThrowsArgumentException()
    {
        // Arrange
        var identifier = "users; DROP TABLE users";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => SqliteExplorerValidation.EnsureValidIdentifier(identifier));
        Assert.Contains("Identifier is invalid", exception.Message);
        Assert.Contains("SQL injection attempt", exception.Message);
    }

    [Fact]
    public void EnsureValidIdentifier_WithNullIdentifier_ThrowsArgumentNullException()
    {
        // Arrange
        string? identifier = null;

        // Act & Assert - EnsureValidIdentifier throws for null
        var exception = Assert.Throws<ArgumentNullException>(() => SqliteExplorerValidation.EnsureValidIdentifier(identifier!));
        Assert.Equal("identifier", exception.ParamName);
    }

    #endregion
}