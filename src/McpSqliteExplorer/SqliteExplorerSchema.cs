using System.Text;
using Microsoft.Data.Sqlite;

namespace McpSqliteExplorer;

/// <summary>
/// Schema-exploration half of <see cref="SqliteExplorer"/>: indexes, foreign keys,
/// relationship graphs, ERD generation and EF Core migration history. Everything
/// here reads exclusively from PRAGMA calls and <c>sqlite_master</c> over the same
/// read-only connection as the rest of the class.
/// </summary>
public sealed partial class SqliteExplorer
{
    /// <summary>
    /// Lists the indexes defined on a table using <c>PRAGMA index_list</c> and
    /// <c>PRAGMA index_info</c>, including implicit indexes SQLite creates for
    /// UNIQUE constraints and primary keys.
    /// </summary>
    public IReadOnlyList<IndexInfo> ListIndexes(string table) => GetIndexes(table);

    /// <summary>
    /// Lists the foreign keys declared on a table using <c>PRAGMA foreign_key_list</c>.
    /// One entry per referencing column (composite keys produce one entry per column pair).
    /// </summary>
    public IReadOnlyList<ForeignKeyInfo> ListForeignKeys(string table) => GetForeignKeys(table);

    /// <summary>
    /// Walks the foreign-key graph outward from a starting table, in both directions
    /// (tables it references and tables that reference it), up to
    /// <paramref name="maxDepth"/> hops. Useful for answering "what is connected to
    /// this table?" without the agent reconstructing the graph itself.
    /// </summary>
    public IReadOnlyList<ForeignKeyHop> ExploreForeignKeyChain(string table, int maxDepth = 3)
    {
        var safeName = RequireExistingTable(table);
        if (maxDepth < 1)
            maxDepth = 1;

        var edges = _catalog.GetForeignKeyGraph();
        var hops = new List<ForeignKeyHop>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { safeName };
        var frontier = new List<string> { safeName };

        for (var depth = 1; depth <= maxDepth && frontier.Count > 0; depth++)
        {
            var next = new List<string>();
            foreach (var current in frontier)
            {
                foreach (var edge in edges)
                {
                    if (string.Equals(edge.Table, current, StringComparison.OrdinalIgnoreCase))
                    {
                        hops.Add(new ForeignKeyHop(
                            depth, current, edge.Column, edge.ReferencesTable, edge.ReferencesColumn, "references"));
                        if (visited.Add(edge.ReferencesTable))
                            next.Add(edge.ReferencesTable);
                    }

                    if (string.Equals(edge.ReferencesTable, current, StringComparison.OrdinalIgnoreCase))
                    {
                        hops.Add(new ForeignKeyHop(
                            depth, current, edge.ReferencesColumn, edge.Table, edge.Column, "referenced-by"));
                        if (visited.Add(edge.Table))
                            next.Add(edge.Table);
                    }
                }
            }

            frontier = next;
        }

        return hops;
    }

    /// <summary>
    /// Renders the schema as a Mermaid <c>erDiagram</c>: every table with its columns
    /// (PK/FK markers included) and one relationship line per foreign key. The output
    /// can be pasted straight into any Mermaid renderer.
    /// </summary>
    public string GenerateErd()
    {
        var builder = new StringBuilder();
        builder.AppendLine("erDiagram");

        var tables = ListTables().Where(t => t.Type == "table").ToList();
        var allForeignKeys = new List<ForeignKeyInfo>();

        foreach (var table in tables)
        {
            var columns = DescribeTable(table.Name);
            var foreignKeys = ListForeignKeys(table.Name);
            allForeignKeys.AddRange(foreignKeys);

            var fkColumns = foreignKeys
                .Select(fk => fk.Column)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            builder.AppendLine($" {MermaidName(table.Name)} {{");
            foreach (var column in columns)
            {
                var type = string.IsNullOrWhiteSpace(column.Type)
                    ? "ANY"
                    : column.Type.Replace(' ', '_').Replace('(', '_').Replace(")", "");
                var markers = new List<string>();
                if (column.PrimaryKey)
                    markers.Add("PK");
                if (fkColumns.Contains(column.Name))
                    markers.Add("FK");

                builder.Append($" {type} {MermaidName(column.Name)}");
                if (markers.Count > 0)
                    builder.Append(' ').Append(string.Join(",", markers));
                builder.AppendLine();
            }

            builder.AppendLine(" }");
        }

        foreach (var fk in allForeignKeys)
        {
            var target = fk.ReferencesColumn ?? "PK";
            builder.AppendLine($" {MermaidName(fk.Table)} }}o--|| {MermaidName(fk.ReferencesTable)} : \"{fk.Column} -> {target}\"");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Reads EF Core's <c>__EFMigrationsHistory</c> table if this database was created
    /// by Entity Framework Core. Reports absence explicitly rather than throwing, since
    /// most SQLite files are not EF databases.
    /// </summary>
    public EfMigrationInfo GetMigrationHistory()
    {
        using var connection = OpenConnection();
        using (var probe = connection.CreateCommand())
        {
            probe.CommandText =
                "SELECT name FROM sqlite_master\n" +
                " WHERE type = 'table' AND name = '__EFMigrationsHistory'\n" +
                " LIMIT 1;";
            if (probe.ExecuteScalar() is null)
                return new EfMigrationInfo(HasHistoryTable: false, Migrations: []);
        }

        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT MigrationId, ProductVersion\n" +
            " FROM __EFMigrationsHistory\n" +
            " ORDER BY MigrationId;";

        var migrations = new List<MigrationEntry>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            migrations.Add(new MigrationEntry(reader.GetString(0), reader.GetString(1)));

        return new EfMigrationInfo(HasHistoryTable: true, Migrations: migrations);
    }

    /// <summary>
    /// Mermaid identifiers cannot contain spaces or quotes; anything unusual is
    /// replaced with underscores so the diagram still parses.
    /// </summary>
    private static string MermaidName(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
            builder.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        return builder.Length > 0 ? builder.ToString() : "_";
    }
}

public sealed record IndexInfo(
    string Name,
    string Table,
    bool Unique,
    string Origin,
    bool Partial,
    IReadOnlyList<string> Columns);

public sealed record ForeignKeyInfo(
    string Table,
    string Column,
    string ReferencesTable,
    string? ReferencesColumn,
    string OnUpdate,
    string OnDelete);

public sealed record ForeignKeyHop(
    int Depth,
    string FromTable,
    string? FromColumn,
    string ToTable,
    string? ToColumn,
    string Direction);

public sealed record EfMigrationInfo(
    bool HasHistoryTable,
    IReadOnlyList<MigrationEntry> Migrations);

public sealed record MigrationEntry(string MigrationId, string ProductVersion);