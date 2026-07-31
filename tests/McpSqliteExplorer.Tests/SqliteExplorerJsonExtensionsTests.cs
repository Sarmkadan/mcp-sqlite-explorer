using System;
using McpSqliteExplorer;
using Xunit;

namespace McpSqliteExplorer.Tests
{
    /// <summary>
    /// Tests for the <see cref="SqliteExplorerJsonExtensions"/> class.
    /// </summary>
    public sealed class SqliteExplorerJsonExtensionsTests
    {
        /// <summary>
        /// Verifies that ToJson() returns a valid JSON string when given a valid SqliteExplorer instance.
        /// </summary>
        [Fact]
        public void ToJson_WithValidExplorer_ReturnsValidJsonString()
        {
            var explorer = new SqliteExplorer("/tmp/test.db");
            var json = explorer.ToJson();

            Assert.NotNull(json);
            Assert.NotEmpty(json);
            Assert.StartsWith("{", json);
            Assert.EndsWith("}", json);
        }

        /// <summary>
        /// Verifies that ToJson(indented: true) returns formatted JSON with newlines.
        /// </summary>
        [Fact]
        public void ToJson_WithIndentedTrue_ReturnsFormattedJson()
        {
            var explorer = new SqliteExplorer("/tmp/test.db");
            var json = explorer.ToJson(indented: true);

            Assert.NotNull(json);
            Assert.Contains("\n", json);
        }

        /// <summary>
        /// Verifies that ToJson(indented: false) returns compact JSON with minimal whitespace.
        /// </summary>
        [Fact]
        public void ToJson_WithIndentedFalse_ReturnsCompactJson()
        {
            var explorer = new SqliteExplorer("/tmp/test.db");
            var json = explorer.ToJson(indented: false);

            Assert.NotNull(json);
            var compactLength = json.Replace(" ", "").Replace("\n", "").Replace("\r", "").Length;
            Assert.True(compactLength < json.Length * 0.9);
        }

        /// <summary>
        /// Verifies that ToJson() throws an ArgumentNullException when the SqliteExplorer instance is null.
        /// </summary>
        [Fact]
        public void ToJson_WithNullValue_ThrowsArgumentNullException()
        {
            SqliteExplorer nullExplorer = null!;
            var ex = Assert.Throws<ArgumentNullException>(() => nullExplorer.ToJson());
            Assert.Equal("value", ex.ParamName);
        }

        /// <summary>
        /// Verifies that ToJson(indented: true) throws an ArgumentNullException when the SqliteExplorer instance is null.
        /// </summary>
        [Fact]
        public void ToJson_WithNullValueAndIndented_ThrowsArgumentNullException()
        {
            SqliteExplorer nullExplorer = null!;
            var ex = Assert.Throws<ArgumentNullException>(() => nullExplorer.ToJson(indented: true));
            Assert.Equal("value", ex.ParamName);
        }

        /// <summary>
        /// Verifies that FromJson(string) throws an ArgumentNullException when the JSON string is null.
        /// </summary>
        [Fact]
        public void FromJson_WithNullJson_ThrowsArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => SqliteExplorerJsonExtensions.FromJson(null!));
            Assert.Equal("json", ex.ParamName);
        }

        /// <summary>
        /// Verifies that TryFromJson(string, out SqliteExplorer?) throws an ArgumentNullException when the JSON string is null.
        /// </summary>
        [Fact]
        public void TryFromJson_WithNullJson_ThrowsArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => SqliteExplorerJsonExtensions.TryFromJson(null!, out _));
            Assert.Equal("json", ex.ParamName);
        }

        /// <summary>
        /// Verifies that FromJson(string) returns null when given an empty JSON string.
        /// </summary>
        [Fact]
        public void FromJson_WithEmptyJson_ReturnsNull()
        {
            var result = SqliteExplorerJsonExtensions.FromJson("");
            Assert.Null(result);
        }

        /// <summary>
        /// Verifies that TryFromJson(string, out SqliteExplorer?) returns false and null when given an empty JSON string.
        /// </summary>
        [Fact]
        public void TryFromJson_WithEmptyJson_ReturnsFalseAndNull()
        {
            var result = SqliteExplorerJsonExtensions.TryFromJson("", out var value);
            Assert.False(result);
            Assert.Null(value);
        }

        /// <summary>
        /// Verifies that FromJson(string) returns null when given a JSON string containing only whitespace.
        /// </summary>
        [Fact]
        public void FromJson_WithWhitespaceJson_ReturnsNull()
        {
            var result = SqliteExplorerJsonExtensions.FromJson("   \n\t  ");
            Assert.Null(result);
        }

        /// <summary>
        /// Verifies that TryFromJson(string, out SqliteExplorer?) returns false and null when given a JSON string containing only whitespace.
        /// </summary>
        [Fact]
        public void TryFromJson_WithWhitespaceJson_ReturnsFalseAndNull()
        {
            var result = SqliteExplorerJsonExtensions.TryFromJson("   \n\t  ", out var value);
            Assert.False(result);
            Assert.Null(value);
        }

        /// <summary>
        /// Verifies that FromJson(string) returns null when given an invalid JSON string.
        /// </summary>
        [Fact]
        public void FromJson_WithInvalidJson_ReturnsNull()
        {
            var result = SqliteExplorerJsonExtensions.FromJson("not valid json");
            Assert.Null(result);
        }

        /// <summary>
        /// Verifies that TryFromJson(string, out SqliteExplorer?) returns false and null when given an invalid JSON string.
        /// </summary>
        [Fact]
        public void TryFromJson_WithInvalidJson_ReturnsFalseAndNull()
        {
            var result = SqliteExplorerJsonExtensions.TryFromJson("not valid json", out var value);
            Assert.False(result);
            Assert.Null(value);
        }
    }
}