using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using McpSqliteExplorer;
using Xunit;

namespace McpSqliteExplorer.Tests
{
    /// <summary>
    /// Tests for the extension methods defined in <see cref="SqliteToolsExtensions"/>.
    /// The tests use a real SQLite file created on‑the‑fly so that the underlying
    /// <see cref="SqliteTools"/> methods exercise the actual implementation.
    /// </summary>
    public sealed class SqliteToolsExtensionsTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly SqliteExplorer _explorer;

        public SqliteToolsExtensionsTests()
        {
            // Create a temporary SQLite file and populate it with a simple schema.
            _dbPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.db");
            using var conn = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = _dbPath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                    Cache = SqliteCacheMode.Shared
                }.ToString());

            conn.Open();

            // Table with three columns and three rows.
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE Sample (
                        Id   INTEGER PRIMARY KEY,
                        Name TEXT NOT NULL,
                        Age  INTEGER
                    );

                    INSERT INTO Sample (Name, Age) VALUES ('Alice', 30);
                    INSERT INTO Sample (Name, Age) VALUES ('Bob',   25);
                    INSERT INTO Sample (Name, Age) VALUES ('Carol', 27);
                ";
                cmd.ExecuteNonQuery();
            }

            // A simple view.
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE VIEW SampleView AS SELECT Id, Name FROM Sample;
                ";
                cmd.ExecuteNonQuery();
            }

            // Close the write connection – the explorer will open it read‑only.
            conn.Close();

            // Initialise the read‑only explorer that the extension methods will use.
            _explorer = new SqliteExplorer(_dbPath);
        }

        public void Dispose()
        {
            _explorer?.Dispose();

            try
            {
                if (File.Exists(_dbPath))
                    File.Delete(_dbPath);
            }
            catch
            {
                // Swallow any cleanup errors – they are not relevant to the test results.
            }
        }

        #region Happy‑path tests

        [Fact]
        public void GetTableCount_ReturnsJsonWithTablesArray()
        {
            var json = _explorer.GetTableCount();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("tables", out var tables));
            Assert.True(tables.ValueKind == JsonValueKind.Array);
            // We created one table and one view, both appear in ListTables.
            Assert.Equal(2, tables.GetArrayLength());
        }

        [Fact]
        public void GetRowCount_ReturnsCorrectCount()
        {
            var json = _explorer.GetRowCount("Sample");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("rows", out var rows));
            Assert.Equal(JsonValueKind.Array, rows.ValueKind);
            Assert.Single(rows.EnumerateArray());

            var firstRow = rows[0];
            Assert.True(firstRow.TryGetProperty("row_count", out var countProp));
            Assert.Equal(3, countProp.GetInt32()); // three rows were inserted.
        }

        [Fact]
        public void GetAllColumns_ReturnsAllColumnsAcrossTables()
        {
            var json = _explorer.GetAllColumns();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("totalColumns", out var totalColumnsProp));
            // Sample table has 3 columns, SampleView has 2 columns (Id, Name).
            // GetAllColumns iterates over tables (including views) and extracts columns
            // via SqliteTools.DescribeTable, which returns the columns of the underlying
            // table/view. Therefore we expect 5 total columns.
            Assert.Equal(5, totalColumnsProp.GetInt32());

            Assert.True(root.TryGetProperty("columns", out var columnsProp));
            Assert.Equal(5, columnsProp.GetArrayLength());
        }

        [Fact]
        public void GetSchemaSummary_ReturnsCorrectCounts()
        {
            var json = _explorer.GetSchemaSummary();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("tables", out var tablesProp));
            Assert.Equal(1, tablesProp.GetInt32()); // one real table

            Assert.True(root.TryGetProperty("views", out var viewsProp));
            Assert.Equal(1, viewsProp.GetInt32()); // one view

            Assert.True(root.TryGetProperty("totalColumns", out var totalColumnsProp));
            // Sample (3 cols) + SampleView (2 cols) = 5
            Assert.Equal(5, totalColumnsProp.GetInt32());

            Assert.True(root.TryGetProperty("totalObjects", out var totalObjectsProp));
            // Two objects (table + view)
            Assert.Equal(2, totalObjectsProp.GetInt32());
        }

        #endregion

        #region Argument validation tests

        [Fact]
        public void GetTableCount_NullExplorer_ThrowsArgumentNullException()
        {
            SqliteExplorer nullExplorer = null!;
            var ex = Assert.Throws<ArgumentNullException>(() => SqliteToolsExtensions.GetTableCount(nullExplorer));
            Assert.Equal("explorer", ex.ParamName);
        }

        [Fact]
        public void GetRowCount_NullExplorer_ThrowsArgumentNullException()
        {
            SqliteExplorer nullExplorer = null!;
            var ex = Assert.Throws<ArgumentNullException>(() => SqliteToolsExtensions.GetRowCount(nullExplorer, "Sample"));
            Assert.Equal("explorer", ex.ParamName);
        }

        [Fact]
        public void GetRowCount_NullOrEmptyTable_ThrowsArgumentException()
        {
            var ex1 = Assert.Throws<ArgumentException>(() => _explorer.GetRowCount(null!));
            Assert.Equal("table", ex1.ParamName);

            var ex2 = Assert.Throws<ArgumentException>(() => _explorer.GetRowCount(string.Empty));
            Assert.Equal("table", ex2.ParamName);
        }

        [Fact]
        public void GetAllColumns_NullExplorer_ThrowsArgumentNullException()
        {
            SqliteExplorer nullExplorer = null!;
            var ex = Assert.Throws<ArgumentNullException>(() => SqliteToolsExtensions.GetAllColumns(nullExplorer));
            Assert.Equal("explorer", ex.ParamName);
        }

        [Fact]
        public void GetSchemaSummary_NullExplorer_ThrowsArgumentNullException()
        {
            SqliteExplorer nullExplorer = null!;
            var ex = Assert.Throws<ArgumentNullException>(() => SqliteToolsExtensions.GetSchemaSummary(nullExplorer));
            Assert.Equal("explorer", ex.ParamName);
        }

        #endregion
    }
}
