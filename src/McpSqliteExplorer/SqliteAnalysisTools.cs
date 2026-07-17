using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpSqliteExplorer;

/// <summary>
/// MCP tool surface for schema exploration and analysis. Thin adapters over
/// <see cref="SqliteExplorer"/>, same shape as <see cref="SqliteTools"/>.
/// All operations are read-only.
/// </summary>
[McpServerToolType]
public sealed class SqliteAnalysisTools
{
    [McpServerTool(Name = "list_indexes")]
    [Description("List the indexes on a table: name, uniqueness, origin (explicit, UNIQUE constraint or primary key), partial flag and indexed columns.")]
    public static string ListIndexes(
        SqliteExplorer explorer,
        [Description("Name of the table whose indexes to list.")] string table) =>
        SqliteTools.Guarded(() =>
        {
            var indexes = explorer.ListIndexes(table);
            return new { table, count = indexes.Count, indexes };
        });

    [McpServerTool(Name = "list_foreign_keys")]
    [Description("List the foreign keys declared on a table: referencing column, referenced table/column, and ON UPDATE / ON DELETE actions.")]
    public static string ListForeignKeys(
        SqliteExplorer explorer,
        [Description("Name of the table whose foreign keys to list.")] string table) =>
        SqliteTools.Guarded(() =>
        {
            var foreignKeys = explorer.ListForeignKeys(table);
            return new { table, count = foreignKeys.Count, foreignKeys };
        });

    [McpServerTool(Name = "foreign_key_graph")]
    [Description("Return every foreign-key relationship in the database as a flat edge list - the full relationship graph in one call.")]
    public static string ForeignKeyGraph(SqliteExplorer explorer) =>
        SqliteTools.Guarded(() =>
        {
            var edges = explorer.GetForeignKeyGraph();
            return new { count = edges.Count, edges };
        });

    [McpServerTool(Name = "foreign_key_chain")]
    [Description("Walk the foreign-key graph outward from a table, in both directions (referenced and referencing), up to a maximum depth. Answers 'what is connected to this table?'.")]
    public static string ForeignKeyChain(
        SqliteExplorer explorer,
        [Description("Table to start from.")] string table,
        [Description("Maximum number of hops to follow (default 3).")] int maxDepth = 3) =>
        SqliteTools.Guarded(() =>
        {
            var hops = explorer.ExploreForeignKeyChain(table, maxDepth);
            return new { table, maxDepth, count = hops.Count, hops };
        });

    [McpServerTool(Name = "generate_erd")]
    [Description("Render the whole schema as a Mermaid erDiagram: every table with typed columns (PK/FK markers) plus one relationship line per foreign key. Paste the output into any Mermaid renderer.")]
    public static string GenerateErd(SqliteExplorer explorer) =>
        SqliteTools.Guarded(() => new { format = "mermaid", diagram = explorer.GenerateErd() });

    [McpServerTool(Name = "explain_query_plan")]
    [Description("Run EXPLAIN QUERY PLAN for a read-only SELECT and return SQLite's plan tree (scans, index usage, join order) without executing the query.")]
    public static string ExplainQueryPlan(
        SqliteExplorer explorer,
        [Description("A single SELECT or WITH ... SELECT statement to explain.")] string sql) =>
        SqliteTools.Guarded(() =>
        {
            var plan = explorer.ExplainQueryPlan(sql);
            return new { sql, nodes = plan };
        });

    [McpServerTool(Name = "profile_table")]
    [Description("Profile every column of a table: row count, null count and rate, distinct cardinality, min/max, and the most frequent values. Computed inside SQLite, so it is safe on large tables.")]
    public static string ProfileTable(
        SqliteExplorer explorer,
        [Description("Name of the table to profile.")] string table) =>
        SqliteTools.Guarded(() => explorer.ProfileTable(table));

    [McpServerTool(Name = "table_stats")]
    [Description("Per-table size overview: row count, column count, index count and on-disk size estimate (when the SQLite build exposes dbstat).")]
    public static string TableStatsOverview(SqliteExplorer explorer) =>
        SqliteTools.Guarded(() =>
        {
            var stats = explorer.GetTableStats();
            return new { count = stats.Count, tables = stats };
        });

    [McpServerTool(Name = "suggest_indexes")]
    [Description("Analyse a SELECT's query plan for full table scans and suggest candidate CREATE INDEX statements for the un-indexed columns the query touches. Suggestions are heuristic - review them before applying; this server can never create the index itself.")]
    public static string SuggestIndexes(
        SqliteExplorer explorer,
        [Description("A single SELECT or WITH ... SELECT statement to analyse.")] string sql) =>
        SqliteTools.Guarded(() =>
        {
            var suggestions = explorer.SuggestIndexes(sql);
            return new { sql, count = suggestions.Count, suggestions };
        });

    [McpServerTool(Name = "migration_history")]
    [Description("If this is an EF Core database, list the applied migrations from __EFMigrationsHistory (migration id and EF product version). Reports hasHistoryTable=false for non-EF databases.")]
    public static string MigrationHistory(SqliteExplorer explorer) =>
        SqliteTools.Guarded(() => explorer.GetMigrationHistory());
}
