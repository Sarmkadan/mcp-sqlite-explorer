using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using McpSqliteExplorer;
using Xunit;

namespace McpSqliteExplorer.Tests
{
    /// <summary>
    /// Tests for the read‑only analysis surface (<see cref="SqliteAnalysisTools"/>).
    /// Each test creates a temporary SQLite file, populates a minimal schema,
    /// runs the tool method and validates that the returned JSON contains the
    /// expected top‑level properties.
    /// </summary>
    public sealed class SqliteAnalysisToolsTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly SqliteExplorer _explorer;

        public SqliteAnalysisToolsTests()
        {
            // Create a temporary file that will be deleted when the test class is disposed.
            _dbPath = Path.GetTempFileName();

            // Initialise a simple schema:
            //   - Table `users` (id PK, name)
            //   - Table `orders` referencing `users` (FK)
            //   - An index on `orders(user_id)`
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(
                new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                {
                    DataSource = _dbPath,
                    Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
                    Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Shared
                }.ToString());

            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE users (
                    id   INTEGER PRIMARY KEY,
                    name TEXT NOT NULL
                );

                CREATE TABLE orders (
                    id      INTEGER PRIMARY KEY,
                    user_id INTEGER NOT NULL,
                    amount  REAL,
                    FOREIGN KEY (user_id) REFERENCES users(id)
                );

                CREATE INDEX idx_orders_user_id ON orders(user_id);
            ";
            cmd.ExecuteNonQuery();
        }

        public void Dispose()
        {
            _explorer?.Dispose();
            try { File.Delete(_dbPath); } catch { /* ignore */ }
        }

        private SqliteExplorer Explorer
        {
            get
            {
                // Lazily create the explorer so the file is ready.
                if (_explorer == null)
                {
                    // Re‑assign to a new instance each time; the explorer is cheap.
                    var explorer = new SqliteExplorer(_dbPath);
                    // Store in the field for disposal.
                    typeof(SqliteAnalysisToolsTests)
                        .GetField("_explorer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                        .SetValue(this, explorer);
                }

                return _explorer!;
            }
        }

        private static JsonDocument Parse(string json) => JsonDocument.Parse(json);

        [Fact]
        public void ListIndexes_ReturnsIndexes_ForExistingTable()
        {
            var json = SqliteAnalysisTools.ListIndexes(Explorer, "orders");
            using var doc = Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("table", out var tableProp));
            Assert.Equal("orders", tableProp.GetString());

            Assert.True(root.TryGetProperty("count", out var countProp));
            Assert.True(countProp.GetInt32() >= 1); // at least the explicit index we created

            Assert.True(root.TryGetProperty("indexes", out var indexesProp));
            Assert.Equal(JsonValueKind.Array, indexesProp.ValueKind);
        }

        [Fact]
        public void ListForeignKeys_ReturnsForeignKeys_ForTableWithFk()
        {
            var json = SqliteAnalysisTools.ListForeignKeys(Explorer, "orders");
            using var doc = Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("table", out var tableProp));
            Assert.Equal("orders", tableProp.GetString());

            Assert.True(root.TryGetProperty("count", out var countProp));
            Assert.Equal(1, countProp.GetInt32());

            Assert.True(root.TryGetProperty("foreignKeys", out var fkProp));
            Assert.Equal(JsonValueKind.Array, fkProp.ValueKind);
            Assert.NotEmpty(fkProp.EnumerateArray());
        }

        [Fact]
        public void ForeignKeyGraph_ReturnsEdges_EvenWhenEmpty()
        {
            var json = SqliteAnalysisTools.ForeignKeyGraph(Explorer);
            using var doc = Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("count", out var countProp));
            Assert.Equal(1, countProp.GetInt32()); // one FK edge we created

            Assert.True(root.TryGetProperty("edges", out var edgesProp));
            Assert.Equal(JsonValueKind.Array, edgesProp.ValueKind);
        }

        [Fact]
        public void ForeignKeyChain_ReturnsHops_ForStartTable()
        {
            var json = SqliteAnalysisTools.ForeignKeyChain(Explorer, "orders", maxDepth: 2);
            using var doc = Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("table", out var tableProp));
            Assert.Equal("orders", tableProp.GetString());

            Assert.True(root.TryGetProperty("maxDepth", out var depthProp));
            Assert.Equal(2, depthProp.GetInt32());

            Assert.True(root.TryGetProperty("count", out var countProp));
            Assert.True(countProp.GetInt32() >= 1);

            Assert.True(root.TryGetProperty("hops", out var hopsProp));
            Assert.Equal(JsonValueKind.Array, hopsProp.ValueKind);
        }

        [Fact]
        public void GenerateErd_ReturnsMermaidDiagram()
        {
            var json = SqliteAnalysisTools.GenerateErd(Explorer);
            using var doc = Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("format", out var fmt));
            Assert.Equal("mermaid", fmt.GetString());

            Assert.True(root.TryGetProperty("diagram", out var diagram));
            Assert.False(string.IsNullOrWhiteSpace(diagram.GetString()));
        }

        [Fact]
        public void ExplainQueryPlan_ReturnsPlanNodes()
        {
            var sql = "SELECT * FROM users WHERE id = 1";
            var json = SqliteAnalysisTools.ExplainQueryPlan(Explorer, sql);
            using var doc = Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("sql", out var sqlProp));
            Assert.Equal(sql, sqlProp.GetString());

            Assert.True(root.TryGetProperty("nodes", out var nodesProp));
            Assert.Equal(JsonValueKind.Array, nodesProp.ValueKind);
        }

        [Fact]
        public void ProfileTable_ReturnsProfileObject()
        {
            var json = SqliteAnalysisTools.ProfileTable(Explorer, "users");
            using var doc = Parse(json);
            var root = doc.RootElement;

            // The profile tool returns whatever SqliteExplorer.ProfileTable returns.
            // We only assert that the JSON is an object (not null) and contains at least one property.
            Assert.Equal(JsonValueKind.Object, root.ValueKind);
            Assert.True(root.EnumerateObject().MoveNext());
        }

        [Fact]
        public void TableStatsOverview_ReturnsStats()
        {
            var json = SqliteAnalysisTools.TableStatsOverview(Explorer);
            using var doc = Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("count", out var countProp));
            Assert.True(countProp.GetInt32() >= 2); // users + orders

            Assert.True(root.TryGetProperty("tables", out var tablesProp));
            Assert.Equal(JsonValueKind.Array, tablesProp.ValueKind);
        }

        [Fact]
        public void SuggestIndexes_ReturnsEmptyWhenNoFullScans()
        {
            var sql = "SELECT name FROM users WHERE id = 1";
            var json = SqliteAnalysisTools.SuggestIndexes(Explorer, sql);
            using var doc = Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("sql", out var sqlProp));
            Assert.Equal(sql, sqlProp.GetString());

            Assert.True(root.TryGetProperty("count", out var countProp));
            // With the simple query we expect zero suggestions.
            Assert.Equal(0, countProp.GetInt32());

            Assert.True(root.TryGetProperty("suggestions", out var suggProp));
            Assert.Equal(JsonValueKind.Array, suggProp.ValueKind);
            Assert.Empty(suggProp.EnumerateArray());
        }

        [Fact]
        public void MigrationHistory_ReturnsHasHistoryTableFalse_OnNonEfDb()
        {
            var json = SqliteAnalysisTools.MigrationHistory(Explorer);
            using var doc = Parse(json);
            var root = doc.RootElement;

            // The result contains a property `hasHistoryTable` when the DB is not EF.
            Assert.True(root.TryGetProperty("hasHistoryTable", out var hasProp));
            Assert.False(hasProp.GetBoolean());
        }
    }
}
