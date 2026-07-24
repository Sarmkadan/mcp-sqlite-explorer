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
    /// Tests for the static surface class <see cref="SqliteAnalysisTools"/>.
    /// The tests use a real temporary SQLite file so that the underlying
    /// <see cref="SqliteExplorer"/> works against an actual database.
    /// </summary>
    public sealed class SqliteAnalysisToolsTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly SqliteExplorer _explorer;

        public SqliteAnalysisToolsTests()
        {
            // Create a temporary file for the SQLite database.
            _dbPath = Path.GetTempFileName();

            // Initialise the database schema needed for the tests.
            using var conn = new SqliteConnection($"Data Source={_dbPath};Mode=ReadWriteCreate");
            conn.Open();

            using var cmd = conn.CreateCommand();

            // Simple table with an index and a foreign key.
            cmd.CommandText = @"
                CREATE TABLE parent (
                    id INTEGER PRIMARY KEY,
                    name TEXT NOT NULL
                );

                CREATE TABLE child (
                    id INTEGER PRIMARY KEY,
                    parent_id INTEGER NOT NULL,
                    value TEXT,
                    FOREIGN KEY(parent_id) REFERENCES parent(id)
                );

                CREATE INDEX idx_child_value ON child(value);
            ";
            cmd.ExecuteNonQuery();

            // Insert a few rows for profiling / stats.
            cmd.CommandText = @"
                INSERT INTO parent (name) VALUES ('Alice'), ('Bob');
                INSERT INTO child (parent_id, value) VALUES (1, 'x'), (1, 'y'), (2, 'z');
            ";
            cmd.ExecuteNonQuery();

            // No EF migrations table – this is intentional for the migration history test.
            conn.Close();

            // Initialise the explorer that the tests will use.
            _explorer = new SqliteExplorer(_dbPath);
        }

        public void Dispose()
        {
            _explorer?.Dispose();
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }

        private static JsonElement ParseJson(string json) =>
            JsonDocument.Parse(json).RootElement;

        [Fact]
        public void ListIndexes_ReturnsIndexesForTable()
        {
            var json = SqliteAnalysisTools.ListIndexes(_explorer, "child");
            var root = ParseJson(json);

            Assert.Equal("child", root.GetProperty("table").GetString());
            Assert.True(root.GetProperty("count").GetInt32() >= 1);
            var indexes = root.GetProperty("indexes");
            Assert.True(indexes.GetArrayLength() >= 1);
        }

        [Fact]
        public void ListForeignKeys_ReturnsForeignKeyInfo()
        {
            var json = SqliteAnalysisTools.ListForeignKeys(_explorer, "child");
            var root = ParseJson(json);

            Assert.Equal("child", root.GetProperty("table").GetString());
            var fks = root.GetProperty("foreignKeys");
            Assert.True(fks.GetArrayLength() >= 1);
        }

        [Fact]
        public void ForeignKeyGraph_ContainsExpectedEdge()
        {
            var json = SqliteAnalysisTools.ForeignKeyGraph(_explorer);
            var root = ParseJson(json);

            var edges = root.GetProperty("edges");
            Assert.NotEmpty(edges.EnumerateArray());

            // Verify that there is an edge from child.parent_id -> parent.id
            bool found = false;
            foreach (var edge in edges.EnumerateArray())
            {
                if (edge.GetProperty("fromTable").GetString() == "child" &&
                    edge.GetProperty("toTable").GetString() == "parent")
                {
                    found = true;
                    break;
                }
            }

            Assert.True(found, "Expected foreign‑key edge not found in graph.");
        }

        [Fact]
        public void ForeignKeyChain_RespectsDepth()
        {
            // Depth 1 should still include the direct parent relationship.
            var json = SqliteAnalysisTools.ForeignKeyChain(_explorer, "child", maxDepth: 1);
            var root = ParseJson(json);

            var hops = root.GetProperty("hops");
            Assert.NotEmpty(hops.EnumerateArray());

            // Ensure the chain does not contain unrelated tables.
            foreach (var hop in hops.EnumerateArray())
            {
                var table = hop.GetProperty("table").GetString();
                Assert.True(table == "child" || table == "parent");
            }
        }

        [Fact]
        public void GenerateErd_ReturnsMermaidDiagram()
        {
            var json = SqliteAnalysisTools.GenerateErd(_explorer);
            var root = ParseJson(json);

            Assert.Equal("mermaid", root.GetProperty("format").GetString());
            var diagram = root.GetProperty("diagram").GetString();
            Assert.Contains("classDiagram", diagram); // basic sanity check
            Assert.Contains("parent", diagram);
            Assert.Contains("child", diagram);
        }

        [Fact]
        public void ExplainQueryPlan_ReturnsPlanNodes()
        {
            var sql = "SELECT * FROM child WHERE value = 'x'";
            var json = SqliteAnalysisTools.ExplainQueryPlan(_explorer, sql);
            var root = ParseJson(json);

            Assert.Equal(sql, root.GetProperty("sql").GetString());
            var nodes = root.GetProperty("nodes");
            Assert.NotNull(nodes);
        }

        [Fact]
        public void ProfileTable_ReturnsProfile()
        {
            var json = SqliteAnalysisTools.ProfileTable(_explorer, "child");
            var root = ParseJson(json);

            // The profile is a complex object; we just verify that it contains the table name.
            Assert.Equal("child", root.GetProperty("table").GetString());
        }

        [Fact]
        public void TableStatsOverview_ReturnsStats()
        {
            var json = SqliteAnalysisTools.TableStatsOverview(_explorer);
            var root = ParseJson(json);

            var tables = root.GetProperty("tables");
            Assert.NotEmpty(tables.EnumerateArray());

            // Verify that the stats for the 'child' table are present.
            bool found = false;
            foreach (var tbl in tables.EnumerateArray())
            {
                if (tbl.GetProperty("name").GetString() == "child")
                {
                    found = true;
                    break;
                }
            }

            Assert.True(found, "Child table stats not found.");
        }

        [Fact]
        public void SuggestIndexes_ReturnsSuggestionsForFullScan()
        {
            // Query that scans the whole child table (no WHERE on indexed column)
            var sql = "SELECT * FROM child";
            var json = SqliteAnalysisTools.SuggestIndexes(_explorer, sql);
            var root = ParseJson(json);

            Assert.Equal(sql, root.GetProperty("sql").GetString());
            var suggestions = root.GetProperty("suggestions");
            // May be empty if the heuristic decides no index is needed, but the JSON must be present.
            Assert.NotNull(suggestions);
        }

        [Fact]
        public void MigrationHistory_ReportsNoHistoryTable()
        {
            var json = SqliteAnalysisTools.MigrationHistory(_explorer);
            var root = ParseJson(json);

            // The result should contain a boolean flag indicating the presence of the EF history table.
            Assert.True(root.TryGetProperty("hasHistoryTable", out var hasHistory));
            Assert.False(hasHistory.GetBoolean());
        }

        [Fact]
        public void Methods_ThrowWhenExplorerIsNull()
        {
            // All public static methods should throw ArgumentNullException when explorer is null.
            Assert.Throws<ArgumentNullException>(() => SqliteAnalysisTools.ListIndexes(null!, "any"));
            Assert.Throws<ArgumentNullException>(() => SqliteAnalysisTools.ListForeignKeys(null!, "any"));
            Assert.Throws<ArgumentNullException>(() => SqliteAnalysisTools.ForeignKeyGraph(null!));
            Assert.Throws<ArgumentNullException>(() => SqliteAnalysisTools.ForeignKeyChain(null!, "any"));
            Assert.Throws<ArgumentNullException>(() => SqliteAnalysisTools.GenerateErd(null!));
            Assert.Throws<ArgumentNullException>(() => SqliteAnalysisTools.ExplainQueryPlan(null!, "SELECT 1"));
            Assert.Throws<ArgumentNullException>(() => SqliteAnalysisTools.ProfileTable(null!, "any"));
            Assert.Throws<ArgumentNullException>(() => SqliteAnalysisTools.TableStatsOverview(null!));
            Assert.Throws<ArgumentNullException>(() => SqliteAnalysisTools.SuggestIndexes(null!, "SELECT 1"));
            Assert.Throws<ArgumentNullException>(() => SqliteAnalysisTools.MigrationHistory(null!));
        }

        [Fact]
        public void SuggestIndexes_ForeignKeyColumnWithoutIndex_SuggestsIndex()
        {
            // Create a table with a foreign key column that has no index (should suggest one)
            using var conn = new SqliteConnection($"Data Source={_dbPath};Mode=ReadWriteCreate");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE department (
                    id INTEGER PRIMARY KEY,
                    name TEXT NOT NULL
                );

                CREATE TABLE employee (
                    id INTEGER PRIMARY KEY,
                    name TEXT NOT NULL,
                    department_id INTEGER NOT NULL,
                    is_active BOOLEAN DEFAULT 1,
                    FOREIGN KEY(department_id) REFERENCES department(id)
                );
            ";
            cmd.ExecuteNonQuery();
            conn.Close();

            // Query that filters on the unindexed foreign key column
            var sql = "SELECT * FROM employee WHERE department_id = 1";
            var json = SqliteAnalysisTools.SuggestIndexes(_explorer, sql);
            var root = ParseJson(json);

            Assert.Equal(sql, root.GetProperty("sql").GetString());
            var suggestions = root.GetProperty("suggestions");
            var suggestionsArray = suggestions.EnumerateArray().ToList();

            // Should have at least one suggestion for the department_id column
            Assert.NotEmpty(suggestionsArray);
            var suggestion = suggestionsArray[0];
            Assert.Equal("employee", suggestion.GetProperty("Table").GetString());
            var columns = suggestion.GetProperty("Columns");
            Assert.Contains(columns.EnumerateArray(), c => c.GetString().Equals("department_id", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void SuggestIndexes_ColumnWithExistingIndex_DoesNotSuggestAgain()
        {
            // Query that uses an already indexed column should not suggest an index
            var sql = "SELECT * FROM child WHERE value = 'x'";
            var json = SqliteAnalysisTools.SuggestIndexes(_explorer, sql);
            var root = ParseJson(json);

            Assert.Equal(sql, root.GetProperty("sql").GetString());
            var suggestions = root.GetProperty("suggestions");
            var suggestionsArray = suggestions.EnumerateArray().ToList();

            // Should have no suggestions since 'value' column is already indexed
            Assert.Empty(suggestionsArray);
        }

        [Fact]
        public void SuggestIndexes_LowCardinalityBooleanColumn_HandlesGracefully()
        {
            // Create a table with a boolean column (low cardinality)
            using var conn = new SqliteConnection($"Data Source={_dbPath};Mode=ReadWriteCreate");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE settings (
                    id INTEGER PRIMARY KEY,
                    name TEXT NOT NULL UNIQUE,
                    enabled BOOLEAN DEFAULT 1
                );
            ";
            cmd.ExecuteNonQuery();

            // Insert some data
            cmd.CommandText = "INSERT INTO settings (name, enabled) VALUES ('feature_x', 1), ('feature_y', 0), ('feature_z', 1)";
            cmd.ExecuteNonQuery();
            conn.Close();

            // Query that filters on the boolean column
            var sql = "SELECT * FROM settings WHERE enabled = 1";
            var json = SqliteAnalysisTools.SuggestIndexes(_explorer, sql);
            var root = ParseJson(json);

            Assert.Equal(sql, root.GetProperty("sql").GetString());
            var suggestions = root.GetProperty("suggestions");
            var suggestionsArray = suggestions.EnumerateArray().ToList();

            // Should still suggest an index even for low cardinality columns
            // The heuristic should not crash or produce invalid results
            Assert.NotNull(suggestionsArray);
        }

        [Fact]
        public void SuggestIndexes_EmptyTable_HandlesGracefully()
        {
            // Create an empty table
            using var conn = new SqliteConnection($"Data Source={_dbPath};Mode=ReadWriteCreate");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE empty_table (
                    id INTEGER PRIMARY KEY,
                    name TEXT NOT NULL,
                    status TEXT
                );
            ";
            cmd.ExecuteNonQuery();
            conn.Close();

            // Query on empty table should not crash
            var sql = "SELECT * FROM empty_table WHERE status = 'active'";
            var json = SqliteAnalysisTools.SuggestIndexes(_explorer, sql);
            var root = ParseJson(json);

            Assert.Equal(sql, root.GetProperty("sql").GetString());
            var suggestions = root.GetProperty("suggestions");
            // May be empty or contain suggestions - either is acceptable
            Assert.NotNull(suggestions);
        }

        [Fact]
        public void SuggestIndexes_MultipleUnindexedColumns_SuggestsCompositeIndex()
        {
            // Create a table with multiple unindexed columns
            using var conn = new SqliteConnection($"Data Source={_dbPath};Mode=ReadWriteCreate");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE product (
                    id INTEGER PRIMARY KEY,
                    name TEXT NOT NULL,
                    category TEXT NOT NULL,
                    price REAL NOT NULL,
                    in_stock BOOLEAN DEFAULT 1
                );
            ";
            cmd.ExecuteNonQuery();

            // Insert some data
            cmd.CommandText = "INSERT INTO product (name, category, price, in_stock) VALUES ('Widget', 'Tools', 9.99, 1), ('Gadget', 'Tools', 19.99, 0)";
            cmd.ExecuteNonQuery();
            conn.Close();

            // Query that filters on multiple unindexed columns
            var sql = "SELECT * FROM product WHERE category = 'Tools' AND in_stock = 1";
            var json = SqliteAnalysisTools.SuggestIndexes(_explorer, sql);
            var root = ParseJson(json);

            Assert.Equal(sql, root.GetProperty("sql").GetString());
            var suggestions = root.GetProperty("suggestions");
            var suggestionsArray = suggestions.EnumerateArray().ToList();

            // Should suggest a composite index
            Assert.NotEmpty(suggestionsArray);
            var suggestion = suggestionsArray[0];
            Assert.Equal("product", suggestion.GetProperty("Table").GetString());
            var columns = suggestion.GetProperty("Columns");
            var columnList = columns.EnumerateArray().Select(c => c.GetString()).ToList();

            // Should include both filtered columns in the suggestion
            Assert.Contains("category", columnList, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("in_stock", columnList, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void SuggestIndexes_PrimaryKeyColumn_IgnoredInSuggestions()
        {
            // Primary key columns should never be suggested for indexing
            var sql = "SELECT * FROM parent WHERE id = 1";
            var json = SqliteAnalysisTools.SuggestIndexes(_explorer, sql);
            var root = ParseJson(json);

            Assert.Equal(sql, root.GetProperty("sql").GetString());
            var suggestions = root.GetProperty("suggestions");
            var suggestionsArray = suggestions.EnumerateArray().ToList();

            // Should have no suggestions since 'id' is a primary key
            Assert.Empty(suggestionsArray);
        }
    }
}