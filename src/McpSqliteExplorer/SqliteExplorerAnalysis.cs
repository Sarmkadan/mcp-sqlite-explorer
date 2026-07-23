using Microsoft.Data.Sqlite;

namespace McpSqliteExplorer;

/// <summary>
/// Analysis half of <see cref="SqliteExplorer"/>: query-plan explanation, data
/// profiling, table statistics and heuristic index suggestions. Like everything
/// else in this class, all of it runs over the read-only connection.
/// </summary>
public sealed partial class SqliteExplorer
{
    /// <summary>Number of most-frequent values reported per column by profiling.</summary>
    public const int TopValueCount = 5;

    /// <summary>
    /// Runs <c>EXPLAIN QUERY PLAN</c> for a read-only SELECT and returns the plan
    /// tree SQLite would use. The statement itself is never executed, but it is
    /// still guarded so only SELECT/WITH input is accepted.
    /// </summary>
    public IReadOnlyList<QueryPlanNode> ExplainQueryPlan(string sql)
    {
        GuardSelectOnly(sql);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + sql.Trim().TrimEnd(';');

        var nodes = new List<QueryPlanNode>();
        try
        {
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                nodes.Add(new QueryPlanNode(
                    Id: reader.GetInt64(reader.GetOrdinal("id")),
                    Parent: reader.GetInt64(reader.GetOrdinal("parent")),
                    Detail: reader.GetString(reader.GetOrdinal("detail"))));
            }
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException(DescribeSqliteError(ex), ex);
        }

        return nodes;
    }

    /// <summary>
    /// Profiles every column of a table: row count, null count/rate, distinct
    /// cardinality, min/max, and the most frequent values. All numbers are computed
    /// by SQLite itself, so the whole table is never pulled into memory.
    /// </summary>
    public TableProfile ProfileTable(string table)
    {
        var safeName = RequireExistingTable(table);
        var columns = DescribeTable(safeName);
        var quotedTable = QuoteIdentifier(safeName);

        using var connection = OpenConnection();

        long rowCount;
        using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = $"SELECT COUNT(*) FROM {quotedTable};";
            rowCount = Convert.ToInt64(countCommand.ExecuteScalar());
        }

        var profiles = new List<ColumnProfile>();
        foreach (var column in columns)
        {
            var quotedColumn = QuoteIdentifier(column.Name);

            using var statsCommand = connection.CreateCommand();
            statsCommand.CommandText =
                $"""
                SELECT COUNT({quotedColumn}), COUNT(DISTINCT {quotedColumn}),
                       MIN({quotedColumn}), MAX({quotedColumn})
                FROM {quotedTable};
                """;

            long nonNullCount;
            long distinctCount;
            object? min;
            object? max;
            using (var reader = statsCommand.ExecuteReader())
            {
                reader.Read();
                nonNullCount = reader.GetInt64(0);
                distinctCount = reader.GetInt64(1);
                min = reader.IsDBNull(2) ? null : reader.GetValue(2);
                max = reader.IsDBNull(3) ? null : reader.GetValue(3);
            }

            using var topCommand = connection.CreateCommand();
            topCommand.CommandText =
                $"""
                SELECT {quotedColumn} AS value, COUNT(*) AS occurrences
                FROM {quotedTable}
                WHERE {quotedColumn} IS NOT NULL
                GROUP BY {quotedColumn}
                ORDER BY occurrences DESC, value
                LIMIT {TopValueCount};
                """;

            var topValues = new List<ValueFrequency>();
            using (var reader = topCommand.ExecuteReader())
            {
                while (reader.Read())
                    topValues.Add(new ValueFrequency(
                        reader.IsDBNull(0) ? null : reader.GetValue(0),
                        reader.GetInt64(1)));
            }

            var nullCount = rowCount - nonNullCount;
            profiles.Add(new ColumnProfile(
                Name: column.Name,
                Type: column.Type,
                NullCount: nullCount,
                NullRate: rowCount == 0 ? 0 : Math.Round((double)nullCount / rowCount, 4),
                DistinctCount: distinctCount,
                Min: min,
                Max: max,
                TopValues: topValues));
        }

        return new TableProfile(safeName, rowCount, profiles);
    }

    /// <summary>
    /// Returns per-table size estimates: row count, column count, index count and -
    /// when the SQLite build exposes the <c>dbstat</c> virtual table - the number of
    /// bytes the table's pages occupy on disk.
    /// </summary>
    public IReadOnlyList<TableStats> GetTableStats()
    {
        var stats = new List<TableStats>();
        using var connection = OpenConnection();

        foreach (var table in ListTables())
        {
            if (table.Type != "table")
                continue;

            var quoted = QuoteIdentifier(table.Name);

            long rowCount;
            using (var countCommand = connection.CreateCommand())
            {
                countCommand.CommandText = $"SELECT COUNT(*) FROM {quoted};";
                rowCount = Convert.ToInt64(countCommand.ExecuteScalar());
            }

            long? sizeBytes = null;
            try
            {
                using var sizeCommand = connection.CreateCommand();
                sizeCommand.CommandText = "SELECT SUM(pgsize) FROM dbstat WHERE name = $name;";
                sizeCommand.Parameters.AddWithValue("$name", table.Name);
                var raw = sizeCommand.ExecuteScalar();
                if (raw is not null and not DBNull)
                    sizeBytes = Convert.ToInt64(raw);
            }
            catch (SqliteException)
            {
                // dbstat requires SQLITE_ENABLE_DBSTAT_VTAB; absent builds simply
                // report no size estimate instead of failing the whole call.
            }

            stats.Add(new TableStats(
                Table: table.Name,
                RowCount: rowCount,
                ColumnCount: DescribeTable(table.Name).Count,
                IndexCount: ListIndexes(table.Name).Count,
                EstimatedSizeBytes: sizeBytes));
        }

        return stats;
    }

    /// <summary>
    /// Suggests indexes for a SELECT by inspecting its query plan for full table
    /// scans, then cross-referencing which of the scanned table's columns the query
    /// actually mentions. Heuristic by design: the output is a starting point for a
    /// human, phrased as ready-to-run <c>CREATE INDEX</c> DDL (which this server
    /// itself can never execute).
    /// </summary>
    public IReadOnlyList<IndexSuggestion> SuggestIndexes(string sql)
    {
        var plan = ExplainQueryPlan(sql);
        var suggestions = new List<IndexSuggestion>();
        var tableNames = ListTables()
            .Where(t => t.Type == "table")
            .Select(t => t.Name)
            .ToList();

        var sqlTokens = TokenizeIdentifiers(sql);

        foreach (var node in plan)
        {
            // "SCAN books" (or older "SCAN TABLE books") without "USING INDEX" means
            // SQLite walks every row of the table.
            if (!node.Detail.StartsWith("SCAN ", StringComparison.OrdinalIgnoreCase) ||
                node.Detail.Contains("USING INDEX", StringComparison.OrdinalIgnoreCase) ||
                node.Detail.Contains("USING COVERING INDEX", StringComparison.OrdinalIgnoreCase))
                continue;

            var scanned = node.Detail.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Skip(1)
                .FirstOrDefault(part => !part.Equals("TABLE", StringComparison.OrdinalIgnoreCase));
            if (scanned is null)
                continue;

            var table = tableNames.FirstOrDefault(
                t => t.Equals(scanned, StringComparison.OrdinalIgnoreCase));
            if (table is null)
                continue;

            var indexed = ListIndexes(table)
                .SelectMany(i => i.Columns)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var candidates = DescribeTable(table)
                .Where(c => !c.PrimaryKey && !indexed.Contains(c.Name))
                .Where(c => sqlTokens.Contains(c.Name))
                .Select(c => c.Name)
                .ToList();

            if (candidates.Count == 0)
                continue;

            var indexName = $"idx_{table}_{string.Join("_", candidates)}";
            suggestions.Add(new IndexSuggestion(
                Table: table,
                Columns: candidates,
                ProposedSql: $"CREATE INDEX {QuoteIdentifier(indexName)} ON {QuoteIdentifier(table)} ({string.Join(", ", candidates.Select(QuoteIdentifier))});",
                Rationale: $"The plan shows a full scan of '{table}' ({node.Detail.Trim()}) and the query references these un-indexed columns. Verify against real data before creating the index."));
        }

        return suggestions;
    }

    private static HashSet<string> TokenizeIdentifiers(string sql)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = new System.Text.StringBuilder();

        foreach (var c in sql)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                current.Append(c);
            }
            else if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
            tokens.Add(current.ToString());

        return tokens;
    }
}

public sealed record QueryPlanNode(long Id, long Parent, string Detail);

public sealed record TableProfile(
    string Table,
    long RowCount,
    IReadOnlyList<ColumnProfile> Columns);

public sealed record ColumnProfile(
    string Name,
    string Type,
    long NullCount,
    double NullRate,
    long DistinctCount,
    object? Min,
    object? Max,
    IReadOnlyList<ValueFrequency> TopValues);

public sealed record ValueFrequency(object? Value, long Occurrences);

public sealed record TableStats(
    string Table,
    long RowCount,
    int ColumnCount,
    int IndexCount,
    long? EstimatedSizeBytes);

public sealed record IndexSuggestion(
    string Table,
    IReadOnlyList<string> Columns,
    string ProposedSql,
    string Rationale,
IReadOnlyList<QueryPlanNode>? BeforePlan = null,
IReadOnlyList<QueryPlanNode>? AfterPlan = null,
bool PlanUsesIndex = false);
