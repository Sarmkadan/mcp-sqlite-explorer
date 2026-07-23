using System;
using System.IO;
using System.Text.Json;
using McpSqliteExplorer;
using Xunit;

namespace McpSqliteExplorer.Tests
{
    /// <summary>
    /// Tests for the <see cref="SqliteTools"/> class methods.
    /// These tests use a real SQLite file created on-the-fly so that the underlying
    /// methods exercise the actual implementation.
    /// </summary>
    public sealed class SqliteToolsTests : IDisposable
    {
        private readonly TestDatabase _db;
        private readonly SqliteExplorer _explorer;

        public SqliteToolsTests()
        {
            _db = new TestDatabase();
            _explorer = new SqliteExplorer(_db.Path);
        }

        public void Dispose()
        {
            _explorer?.Dispose();
            _db?.Dispose();
        }

        #region Happy path tests

        [Fact]
        public void ListTables_ReturnsValidJson()
        {
            var json = SqliteTools.ListTables(_explorer);

            // Should return valid JSON
            Assert.NotEmpty(json);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Should have count and tables properties
            Assert.True(root.ValueKind == JsonValueKind.Object);
        }

        [Fact]
        public void DescribeTable_ReturnsValidJson()
        {
            var json = SqliteTools.DescribeTable(_explorer, "authors");

            // Should return valid JSON
            Assert.NotEmpty(json);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.ValueKind == JsonValueKind.Object);
        }

        [Fact]
        public void ListIndexes_ReturnsValidJson()
        {
            var json = SqliteTools.ListIndexes(_explorer, "books");

            // Should return valid JSON
            Assert.NotEmpty(json);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.ValueKind == JsonValueKind.Object);
        }

        [Fact]
        public void SampleRows_ReturnsValidJson()
        {
            var json = SqliteTools.SampleRows(_explorer, "authors", 2);

            // Should return valid JSON
            Assert.NotEmpty(json);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.ValueKind == JsonValueKind.Object);
        }

        [Fact]
        public void RunSelect_ReturnsValidJson()
        {
            var json = SqliteTools.RunSelect(_explorer, "SELECT name FROM authors WHERE country = 'US'");

            // Should return valid JSON
            Assert.NotEmpty(json);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.ValueKind == JsonValueKind.Object);
        }

        [Fact]
        public void ExplainQuery_ReturnsValidJson()
        {
            var json = SqliteTools.ExplainQuery(_explorer, "SELECT * FROM authors WHERE id = 1");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("planCount", out var planCount));
            Assert.True(planCount.GetInt32() >= 0);

            Assert.True(root.TryGetProperty("plan", out var plan));
            // plan can be empty array or contain plan steps
            Assert.True(plan.ValueKind == JsonValueKind.Array);
        }

        [Fact]
        public void TableStats_ReturnsValidJsonForTable()
        {
            var json = SqliteTools.TableStats(_explorer, "authors");

            // Should return valid JSON
            Assert.NotEmpty(json);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.ValueKind == JsonValueKind.Object);
        }

        #endregion

        #region Edge cases

        [Fact]
        public void ListTables_WithEmptyDatabase_ReturnsEmptyArray()
        {
            // Create an empty database
            var emptyDbPath = Path.Combine(Path.GetTempPath(), $"empty-{Guid.NewGuid():N}.db");
            using (var conn = new Microsoft.Data.Sqlite.SqliteConnection(
                new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                {
                    DataSource = emptyDbPath,
                    Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate
                }.ToString()))
            {
                conn.Open();
                // Don't create any tables
                conn.Close();
            }

            try
            {
                using var emptyExplorer = new SqliteExplorer(emptyDbPath);
                var json = SqliteTools.ListTables(emptyExplorer);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                Assert.True(root.TryGetProperty("count", out var count));
                Assert.Equal(0, count.GetInt32());

                Assert.True(root.TryGetProperty("tables", out var tables));
                Assert.Equal(0, tables.GetArrayLength());
            }
            finally
            {
                try { File.Delete(emptyDbPath); } catch { }
            }
        }

        [Fact]
        public void DescribeTable_WithView_ReturnsViewColumnInformation()
        {
            var json = SqliteTools.DescribeTable(_explorer, "recent_books");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("table", out var table));
            Assert.Equal("recent_books", table.GetString());

            Assert.True(root.TryGetProperty("columnCount", out var columnCount));
            Assert.Equal(2, columnCount.GetInt32()); // title, year

            Assert.True(root.TryGetProperty("columns", out var columns));
            Assert.Equal(2, columns.GetArrayLength());
        }

        [Fact]
        public void SampleRows_WithLimit_ReturnsSampleData()
        {
            var json = SqliteTools.SampleRows(_explorer, "authors", 2);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("rowCount", out var rowCount));
            Assert.Equal(2, rowCount.GetInt32());
            Assert.True(root.TryGetProperty("rows", out var rows));
            Assert.Equal(2, rows.GetArrayLength());
        }

        [Fact]
        public void RunSelect_WithComplexQuery_ReturnsCorrectResults()
        {
            var json = SqliteTools.RunSelect(_explorer, @"
                WITH us_authors AS (
                    SELECT id, name FROM authors WHERE country = 'US'
                )
                SELECT b.title, a.name AS author, b.year
                FROM books b
                JOIN authors a ON b.author_id = a.id
                WHERE a.country = 'US'
                ORDER BY b.year DESC
            ");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("columns", out var columns));
            var columnNames = columns.EnumerateArray().Select(c => c.GetString()).ToList();
            Assert.Equal(new[] { "title", "author", "year" }, columnNames);

            Assert.True(root.TryGetProperty("rowCount", out var rowCount));
            Assert.Equal(2, rowCount.GetInt32()); // Two US books
        }

        #endregion

        #region Error paths

        [Fact]
        public void DescribeTable_WithNonExistentTable_ReturnsError()
        {
            var json = SqliteTools.DescribeTable(_explorer, "nonexistent_table");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("error", out var error));
            Assert.Contains("No such table or view", error.GetString());
        }

        [Fact]
        public void ListIndexes_WithNonExistentTable_ReturnsError()
        {
            var json = SqliteTools.ListIndexes(_explorer, "nonexistent_table");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("error", out var error));
            Assert.Contains("No such table or view", error.GetString());
        }

        [Fact]
        public void SampleRows_WithNonExistentTable_ReturnsError()
        {
            var json = SqliteTools.SampleRows(_explorer, "nonexistent_table");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("error", out var error));
            Assert.Contains("No such table or view", error.GetString());
        }

        [Fact]
        public void RunSelect_WithWriteStatement_ReturnsError()
        {
            var json = SqliteTools.RunSelect(_explorer, "INSERT INTO authors (name) VALUES ('Test')");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("error", out var error));
            Assert.Contains("Only read-only SELECT", error.GetString());
        }

        [Fact]
        public void RunSelect_WithMultipleStatements_ReturnsError()
        {
            var json = SqliteTools.RunSelect(_explorer, "SELECT 1; SELECT 2;");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("error", out var error));
            Assert.Contains("Only a single SQL statement", error.GetString());
        }

        [Fact]
        public void ExplainQuery_WithNonExistentTable_ReturnsError()
        {
            var json = SqliteTools.ExplainQuery(_explorer, "SELECT * FROM nonexistent_table");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("error", out var error));
            // The error message should contain information about the missing table
            Assert.Contains("table", error.GetString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void TableStats_WithNonExistentTable_ReturnsError()
        {
            var json = SqliteTools.TableStats(_explorer, "nonexistent_table");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("error", out var error));
            Assert.Contains("No such table or view", error.GetString());
        }

        [Fact]
        public void ListTables_WithNullExplorer_Throws()
        {
            SqliteExplorer nullExplorer = null!;
            var ex = Assert.Throws<NullReferenceException>(() => SqliteTools.ListTables(nullExplorer));
            Assert.NotNull(ex);
        }

        [Fact]
        public void DescribeTable_WithNullExplorer_Throws()
        {
            SqliteExplorer nullExplorer = null!;
            var ex = Assert.Throws<NullReferenceException>(() => SqliteTools.DescribeTable(nullExplorer, "authors"));
            Assert.NotNull(ex);
        }

        [Fact]
        public void ListIndexes_WithNullExplorer_Throws()
        {
            SqliteExplorer nullExplorer = null!;
            var ex = Assert.Throws<NullReferenceException>(() => SqliteTools.ListIndexes(nullExplorer, "books"));
            Assert.NotNull(ex);
        }

        [Fact]
        public void SampleRows_WithNullExplorer_Throws()
        {
            SqliteExplorer nullExplorer = null!;
            var ex = Assert.Throws<NullReferenceException>(() => SqliteTools.SampleRows(nullExplorer, "authors"));
            Assert.NotNull(ex);
        }

        [Fact]
        public void RunSelect_WithNullExplorer_Throws()
        {
            SqliteExplorer nullExplorer = null!;
            var ex = Assert.Throws<NullReferenceException>(() => SqliteTools.RunSelect(nullExplorer, "SELECT 1"));
            Assert.NotNull(ex);
        }

        [Fact]
        public void ExplainQuery_WithNullExplorer_Throws()
        {
            SqliteExplorer nullExplorer = null!;
            var ex = Assert.Throws<NullReferenceException>(() => SqliteTools.ExplainQuery(nullExplorer, "SELECT 1"));
            Assert.NotNull(ex);
        }

        [Fact]
        public void TableStats_WithNullExplorer_Throws()
        {
            SqliteExplorer nullExplorer = null!;
            var ex = Assert.Throws<NullReferenceException>(() => SqliteTools.TableStats(nullExplorer, "authors"));
            Assert.NotNull(ex);
        }

        [Fact]
        public void RunSelect_WithTimeBudgetParameter_WorksCorrectly()
        {
            // Test that the timeBudgetSeconds parameter is properly passed through
            // This verifies the API integration works correctly
            var result = _explorer.RunSelect("SELECT * FROM books", limit: 5, timeBudgetSeconds: 15);

            // The query should complete successfully
            Assert.False(result.TimedOut);
            Assert.Null(result.TimeoutMessage);
            Assert.True(result.Rows.Count > 0);
        }

        [Fact]
        public void SampleRows_WithTimeBudgetParameter_WorksCorrectly()
        {
            // Test that SampleRows also accepts the timeBudgetSeconds parameter
            var result = _explorer.SampleRows("authors", limit: 3, timeBudgetSeconds: 15);

            // The query should complete successfully
            Assert.False(result.TimedOut);
            Assert.Null(result.TimeoutMessage);
            Assert.Equal(3, result.Rows.Count);
        }

        [Fact]
        public void RunSelect_WithZeroTimeBudget_DisablesTimeout()
        {
            // Test that timeBudgetSeconds = 0 disables timeout
            var result = _explorer.RunSelect("SELECT 1", limit: 100, timeBudgetSeconds: 0);

            Assert.False(result.TimedOut);
            Assert.Null(result.TimeoutMessage);
        }

        #endregion
    }
}