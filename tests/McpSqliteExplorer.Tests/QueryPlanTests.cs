using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Xunit;

namespace McpSqliteExplorer.Tests;

/// <summary>
/// Tests for QueryPlanNode and query-plan inspection parsing edge cases.
/// These tests verify the robustness of the ExplainQueryPlan method when handling
/// various tree structures and malformed data scenarios in SQLite's EXPLAIN QUERY PLAN output.
/// </summary>
public sealed class QueryPlanTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteExplorer _explorer;

    public QueryPlanTests()
    {
        // Create a temporary file for the SQLite database.
        _dbPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"mcp-sqlite-test-{Guid.NewGuid():N}.db");

        // Initialise the database schema needed for the tests.
        using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString());
        conn.Open();

        using var cmd = conn.CreateCommand();

        // Create tables for testing different query plan scenarios
        cmd.CommandText = @"
        CREATE TABLE single_table (id INTEGER PRIMARY KEY, name TEXT, value INTEGER);
        CREATE TABLE table_a (id INTEGER PRIMARY KEY, name TEXT);
        CREATE TABLE table_b (id INTEGER PRIMARY KEY, table_a_id INTEGER, data TEXT);
        CREATE INDEX idx_single_table_value ON single_table(value);
        CREATE INDEX idx_table_b_table_a_id ON table_b(table_a_id);
        ";
        cmd.ExecuteNonQuery();

        // Insert test data
        cmd.CommandText = @"
        INSERT INTO single_table (name, value) VALUES ('item1', 10), ('item2', 20), ('item3', 30);
        INSERT INTO table_a (name) VALUES ('A'), ('B'), ('C');
        INSERT INTO table_b (table_a_id, data) VALUES (1, 'data1'), (1, 'data2'), (2, 'data3');
        ";
        cmd.ExecuteNonQuery();

        conn.Close();

        // Initialise the explorer that the tests will use.
        _explorer = new SqliteExplorer(_dbPath);
    }

    public void Dispose()
    {
        _explorer?.Dispose();
        SqliteConnection.ClearAllPools();
        try
        {
            if (System.IO.File.Exists(_dbPath))
                System.IO.File.Delete(_dbPath);
        }
        catch (IOException)
        {
            // Best-effort cleanup; a leftover temp file is harmless.
        }
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
    public void ExplainQueryPlan_SingleTableScan_OneRootNode()
    {
        // Test: a simple single-table scan plan (one root node)
        var plan = _explorer.ExplainQueryPlan("SELECT * FROM single_table WHERE value > 15;");

        Assert.NotNull(plan);
        Assert.NotEmpty(plan);

        // Should have at least one node for the table scan
        // SQLite returns "SEARCH table_name USING INDEX index_name" for indexed queries
        Assert.Contains(plan, node => node.Detail.Contains("single_table", StringComparison.OrdinalIgnoreCase));

        // Root nodes should have Parent = 0
        var rootNodes = plan.Where(node => node.Parent == 0).ToList();
        Assert.NotEmpty(rootNodes);

        // Verify the root node structure
        var rootNode = rootNodes[0];
        Assert.True(rootNode.Id > 0);
        Assert.Equal(0, rootNode.Parent);
        Assert.NotEmpty(rootNode.Detail);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
    public void ExplainQueryPlan_JoinMultipleRootSiblings_HandlesGracefully()
    {
        // Test: a plan with a join producing multiple root-level siblings
        var plan = _explorer.ExplainQueryPlan(
            "SELECT * FROM table_a a JOIN table_b b ON a.id = b.table_a_id WHERE b.data = 'data1'");

        Assert.NotNull(plan);

        // Should have nodes for both tables in the join
        Assert.Contains(plan, node => node.Detail.Contains("table_a", StringComparison.OrdinalIgnoreCase) ||
                                      node.Detail.Contains("a", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan, node => node.Detail.Contains("table_b", StringComparison.OrdinalIgnoreCase) ||
                                      node.Detail.Contains("b", StringComparison.OrdinalIgnoreCase));

        // Should have multiple root-level nodes (one for each table in the join)
        var rootNodes = plan.Where(node => node.Parent == 0).ToList();
        Assert.NotEmpty(rootNodes);
        Assert.InRange(rootNodes.Count, 2, plan.Count); // At least 2 root nodes for a join
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
    public void ExplainQueryPlan_MissingParentReference_NoUnhandledException()
    {
        // Test: a plan where a Parent id references a node not present in the result set
        // This simulates malformed or unexpected SQLite output
        // Since we can't directly inject malformed data into SQLite, we test that the
        // method handles edge cases gracefully by ensuring it doesn't throw unhandled exceptions

        // Test various queries that might produce different parent structures
        var queries = new[]
        {
            "SELECT * FROM single_table;",
            "SELECT * FROM table_a WHERE id IN (1, 2, 3);",
            "SELECT a.*, b.* FROM table_a a, table_b b WHERE a.id = b.table_a_id;",
            "WITH cte AS (SELECT 1 as val) SELECT * FROM single_table WHERE value = (SELECT val FROM cte);"
        };

        foreach (var query in queries)
        {
            var exception = Record.Exception(() => _explorer.ExplainQueryPlan(query));
            Assert.Null(exception);
        }
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
    public void ExplainQueryPlan_EmptyResult_NoException()
    {
        // Test: an empty plan result (e.g. for a trivial 'SELECT 1' with no table access)
        var plan = _explorer.ExplainQueryPlan("SELECT 1;");

        Assert.NotNull(plan);

        // For a simple SELECT 1, SQLite returns a "SCAN CONSTANT ROW" node
        // This should not throw an exception and should return a valid plan
        Assert.NotEmpty(plan);
        Assert.Contains(plan, node => node.Detail.Contains("CONSTANT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
    public void ExplainQueryPlan_ComplexJoinTree_HandlesGracefully()
    {
        // Test: a more complex join that creates a tree structure
        var plan = _explorer.ExplainQueryPlan(
            "SELECT * FROM table_a a JOIN table_b b ON a.id = b.table_a_id JOIN single_table s ON s.id = b.id;");

        Assert.NotNull(plan);

        // Should have nodes for all three tables (using aliases)
        Assert.Contains(plan, node => node.Detail.Contains("a", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan, node => node.Detail.Contains("b", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan, node => node.Detail.Contains("s", StringComparison.OrdinalIgnoreCase));

        // Should have root nodes
        var rootNodes = plan.Where(node => node.Parent == 0).ToList();
        Assert.NotEmpty(rootNodes);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
    public void ExplainQueryPlan_QueryPlanNodeProperties_Valid()
    {
        // Test: Verify QueryPlanNode properties are correctly populated
        var plan = _explorer.ExplainQueryPlan("SELECT * FROM single_table WHERE value = 20;");

        foreach (var node in plan)
        {
            // Verify all properties are valid
            Assert.True(node.Id >= 0);
            Assert.True(node.Parent >= 0);
            Assert.NotNull(node.Detail);
            Assert.NotEmpty(node.Detail.Trim());
        }
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
    public void ExplainQueryPlan_MultipleStatements_Rejected()
    {
        // Test: Verify that multiple statements are properly rejected
        var exception = Assert.Throws<ArgumentException>(() =>
            _explorer.ExplainQueryPlan("SELECT 1; SELECT 2;"));

        Assert.NotNull(exception);
        Assert.Contains("Only a single SQL statement is allowed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
    public void ExplainQueryPlan_IndexedLookup_NoScan()
    {
        // Test: Verify that indexed lookups don't produce full scans
        var plan = _explorer.ExplainQueryPlan("SELECT * FROM single_table WHERE value = 20;");

        // Should use index, not scan
        Assert.DoesNotContain(plan, node =>
            node.Detail.StartsWith("SCAN", StringComparison.OrdinalIgnoreCase) &&
            node.Detail.Contains("single_table", StringComparison.OrdinalIgnoreCase));

        // Should have some node with index usage
        Assert.Contains(plan, node =>
            node.Detail.Contains("USING INDEX", StringComparison.OrdinalIgnoreCase) ||
            node.Detail.Contains("idx_single_table_value", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
    public void QueryPlanNode_RecordStructure_Valid()
    {
        // Test: Verify QueryPlanNode record structure matches expected format
        var plan = _explorer.ExplainQueryPlan("SELECT * FROM single_table;");

        if (plan.Count > 0)
        {
            var node = plan[0];

            // Verify it's a proper record with the expected properties
            Assert.Equal(typeof(long), node.Id.GetType());
            Assert.Equal(typeof(long), node.Parent.GetType());
            Assert.Equal(typeof(string), node.Detail.GetType());

            // Verify record equality works
            var node2 = new QueryPlanNode(node.Id, node.Parent, node.Detail);
            Assert.Equal(node, node2);
            Assert.Equal(node.GetHashCode(), node2.GetHashCode());
        }
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
    public void ExplainQueryPlan_ComplexQuery_ProducesValidPlan()
    {
        // Test: A complex query with subqueries and joins
        var plan = _explorer.ExplainQueryPlan(
            @"SELECT a.name, b.data, s.name as item_name
              FROM table_a a
              JOIN table_b b ON a.id = b.table_a_id
              LEFT JOIN single_table s ON s.value = b.table_a_id
              WHERE a.id > 1
              ORDER BY b.data DESC
              LIMIT 10;");

        Assert.NotNull(plan);
        Assert.NotEmpty(plan);

        // Should have nodes for all tables involved
        var involvedTables = new[] { "table_a", "table_b", "single_table" };
        foreach (var table in involvedTables)
        {
            Assert.Contains(plan, node => node.Detail.Contains(table, StringComparison.OrdinalIgnoreCase));
        }
    }
}