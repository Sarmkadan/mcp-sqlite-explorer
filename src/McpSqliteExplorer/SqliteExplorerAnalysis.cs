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

    /// <summary>Default row budget for <see cref="ProfileTable"/> before it switches to sampling.</summary>
    public const long DefaultProfileSampleRows = 100_000;

    /// <summary>
    /// Largest row budget <see cref="ProfileTable"/> accepts. Larger requests are rejected
    /// rather than scanned, so a single call cannot read an arbitrarily large table.
    /// </summary>
    public const long MaxProfileSampleRows = 1_000_000;

    /// <summary>
    /// Maximum number of distinct values tracked in memory per column while profiling.
    /// Columns that stay within this cap get exact distinct counts and exact top-N
    /// frequencies (via a follow-up GROUP BY); columns that exceed it are treated as
    /// high-cardinality and reported with approximate figures gathered during the scan.
    /// </summary>
    private const int ProfileDistinctCap = 2_000;

    /// <summary>
    /// Total number of distinct values retained across all columns of one profiled table.
    /// Keeps a wide table from multiplying <see cref="ProfileDistinctCap"/> by its column count.
    /// </summary>
    private const int ProfileDistinctBudget = 100_000;

    /// <summary>
    /// Longest text (characters) or blob (bytes) value profiling keeps. Longer values are
    /// still counted as non-null but are excluded from min/max, distinct counts and top values,
    /// so a single huge value cannot be retained in memory.
    /// </summary>
    private const int ProfileMaxTrackedValueLength = 256;

    /// <summary>SQLite's <c>SQLITE_INTERRUPT</c> result code, raised when a statement is interrupted.</summary>
    private const int SqliteInterruptCode = 9;

    /// <summary>
    /// Runs <c>EXPLAIN QUERY PLAN</c> for a read-only SELECT and returns the plan
    /// tree SQLite would use. The statement itself is never executed, but it is
    /// still guarded so only SELECT/WITH input is accepted.
    /// </summary>
    public IReadOnlyList<QueryPlanNode> ExplainQueryPlan(string sql)
    {
        ArgumentException.ThrowIfNullOrEmpty(sql);
        GuardSelectOnly(sql);

        return ExecuteWithRetryAsync(() =>
        {
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
        }).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Profiles every column of a table: row count, null count/rate, distinct
    /// cardinality, min/max, and the most frequent values. Null counts, min/max and
    /// approximate distinct counts for all columns are computed together in a single
    /// pass over the row set instead of one query per column; only low-cardinality
    /// columns get a follow-up exact <c>GROUP BY</c> for their top-N frequencies.
    /// Tables larger than <paramref name="sampleRows"/> are profiled from a leading
    /// sample rather than scanned in full, and the result is flagged accordingly.
    /// Text values longer than 256 characters and blobs longer than 256 bytes are counted
    /// as non-null but excluded from min/max, distinct counts and top values. The whole
    /// profile must finish within <see cref="QueryTimeoutSeconds"/> (0 disables the limit).
    /// </summary>
    /// <param name="table">Name of the table to profile.</param>
    /// <param name="sampleRows">
    /// Maximum number of rows to scan. Tables with more rows than this are profiled
    /// from a <c>LIMIT</c>-bounded sample instead of the full table. Defaults to
    /// <see cref="DefaultProfileSampleRows"/>; must not exceed <see cref="MaxProfileSampleRows"/>.
    /// </param>
    /// <returns>The per-column profile of the table.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="table"/> is null/empty, does not exist, or <paramref name="sampleRows"/> is not positive.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sampleRows"/> is greater than <see cref="MaxProfileSampleRows"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">The profile exceeded the time budget, or SQLite reported an error.</exception>
    public TableProfile ProfileTable(string table, long sampleRows = DefaultProfileSampleRows)
    {
        ArgumentException.ThrowIfNullOrEmpty(table);
        if (sampleRows <= 0)
            throw new ArgumentException("Sample row budget must be positive.", nameof(sampleRows));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sampleRows, MaxProfileSampleRows, nameof(sampleRows));

        return ExecuteWithRetryAsync(() => ProfileWithinTimeBudget(table, sampleRows)).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Runs <see cref="ProfileTableCore"/> under a deadline of <see cref="QueryTimeoutSeconds"/>.
    /// The deadline is enforced both between rows and by interrupting the SQLite statement
    /// currently running, so neither a long scan nor a slow GROUP BY can outlive it.
    /// </summary>
    private TableProfile ProfileWithinTimeBudget(string table, long sampleRows)
    {
        var budget = QueryTimeoutSeconds > 0 ? TimeSpan.FromSeconds(QueryTimeoutSeconds) : Timeout.InfiniteTimeSpan;
        using var deadline = new CancellationTokenSource(budget);

        try
        {
            return ProfileTableCore(table, sampleRows, deadline.Token);
        }
        catch (SqliteException ex) when (deadline.IsCancellationRequested && ex.SqliteErrorCode == SqliteInterruptCode)
        {
            throw ProfileTimeoutException();
        }
    }

    private TableProfile ProfileTableCore(string table, long sampleRows, CancellationToken deadline)
    {
        var safeName = RequireExistingTable(table);
        var columns = DescribeTable(safeName);
        var quotedTable = QuoteIdentifier(safeName);
        var quotedColumns = columns.Select(c => QuoteIdentifier(c.Name)).ToList();
        var selectList = string.Join(", ", quotedColumns);

        using var connection = OpenConnection();

        long totalRowCount;
        using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = $"SELECT COUNT(*) FROM {quotedTable};";
            using var interrupt = InterruptOnCancel(countCommand, deadline);
            totalRowCount = Convert.ToInt64(countCommand.ExecuteScalar());
        }

        var isSampled = totalRowCount > sampleRows;

        using var scanCommand = connection.CreateCommand();
        scanCommand.CommandText = isSampled
            ? $"SELECT {selectList} FROM {quotedTable} LIMIT $limit;"
            : $"SELECT {selectList} FROM {quotedTable};";
        if (isSampled)
            scanCommand.Parameters.AddWithValue("$limit", sampleRows);

        var budget = new DistinctBudget(ProfileDistinctBudget);
        var accumulators = columns.Select(_ => new ColumnAccumulator(budget)).ToList();
        long rowsScanned = 0;

        using (var interrupt = InterruptOnCancel(scanCommand, deadline))
        using (var reader = scanCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                ThrowIfExpired(deadline);
                rowsScanned++;
                for (var i = 0; i < accumulators.Count; i++)
                    accumulators[i].Observe(reader.IsDBNull(i) ? null : reader.GetValue(i));
            }
        }

        var profiles = new List<ColumnProfile>(columns.Count);
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var accumulator = accumulators[i];
            var nullCount = rowsScanned - accumulator.NonNullCount;

            var topValues = accumulator.IsLowCardinality
                ? accumulator.TopValues(TopValueCount)
                : QueryExactTopValues(connection, quotedTable, quotedColumns[i], isSampled, sampleRows, deadline);

            profiles.Add(new ColumnProfile(
                Name: column.Name,
                Type: column.Type,
                NullCount: nullCount,
                NullRate: rowsScanned == 0 ? 0.0 : Math.Round((double)nullCount / rowsScanned, 4),
                DistinctCount: accumulator.DistinctCount,
                Min: accumulator.Min,
                Max: accumulator.Max,
                TopValues: topValues));
        }

        return new TableProfile(safeName, totalRowCount, profiles, isSampled, rowsScanned);
    }

    /// <summary>
    /// Runs an exact <c>GROUP BY</c> for a single high-cardinality column's top-N
    /// value frequencies. Reserved for columns whose distinct-value set overflowed
    /// the in-memory cap during the single-pass scan, since a full grouping there
    /// would defeat the point of the cap. Values longer than
    /// <c>ProfileMaxTrackedValueLength</c> are excluded, matching the single-pass scan.
    /// </summary>
    private static IReadOnlyList<ValueFrequency> QueryExactTopValues(
        SqliteConnection connection, string quotedTable, string quotedColumn, bool isSampled, long sampleRows, CancellationToken deadline)
    {
        using var topCommand = connection.CreateCommand();
        var source = isSampled
            ? $"(SELECT {quotedColumn} FROM {quotedTable} LIMIT {sampleRows})"
            : quotedTable;
        topCommand.CommandText =
        $"""
        SELECT {quotedColumn} AS value, COUNT(*) AS occurrences
        FROM {source}
        WHERE {quotedColumn} IS NOT NULL AND length({quotedColumn}) <= {ProfileMaxTrackedValueLength}
        GROUP BY {quotedColumn}
        ORDER BY occurrences DESC, value
        LIMIT {TopValueCount};
        """;

        using var interrupt = InterruptOnCancel(topCommand, deadline);
        var topValues = new List<ValueFrequency>();
        using var reader = topCommand.ExecuteReader();
        while (reader.Read())
            topValues.Add(new ValueFrequency(
                reader.IsDBNull(0) ? null : reader.GetValue(0),
                reader.GetInt64(1)));

        return topValues;
    }

    /// <summary>
    /// Registers an interrupt of <paramref name="command"/> for when <paramref name="deadline"/>
    /// is cancelled. The returned registration must be disposed before the command is.
    /// </summary>
    private static CancellationTokenRegistration InterruptOnCancel(SqliteCommand command, CancellationToken deadline) =>
        deadline.Register(() =>
        {
            try
            {
                command.Cancel();
            }
            catch (InvalidOperationException)
            {
                // The statement already finished; there is nothing left to interrupt.
            }
        });

    /// <summary>Throws the profiling timeout once <paramref name="deadline"/> has been cancelled.</summary>
    private void ThrowIfExpired(CancellationToken deadline)
    {
        if (deadline.IsCancellationRequested)
            throw ProfileTimeoutException();
    }

    private InvalidOperationException ProfileTimeoutException() =>
        new($"Profiling exceeded the time budget of {QueryTimeoutSeconds} seconds. Lower sampleRows or profile a narrower table.");

    /// <summary>
    /// Tracks how many distinct values may still be retained across every column of one
    /// profiled table. Once the budget is spent, further columns fall back to approximate figures.
    /// </summary>
    private sealed class DistinctBudget(int capacity)
    {
        private int _remaining = capacity;

        /// <summary>Reserves one slot, returning false when none remain.</summary>
        public bool TryReserve()
        {
            if (_remaining <= 0)
                return false;

            _remaining--;
            return true;
        }
    }

    /// <summary>
    /// Accumulates null count, min/max and (up to <see cref="ProfileDistinctCap"/>)
    /// exact distinct-value frequencies for one column across a single pass over a
    /// result set.
    /// </summary>
    private sealed class ColumnAccumulator(DistinctBudget budget)
    {
        private readonly Dictionary<object, long> _frequencies = new();

        /// <summary>Number of non-null values observed, including oversized ones.</summary>
        public long NonNullCount { get; private set; }

        /// <summary>Smallest non-null value observed, by best-effort comparison.</summary>
        public object? Min { get; private set; }

        /// <summary>Largest non-null value observed, by best-effort comparison.</summary>
        public object? Max { get; private set; }

        /// <summary>
        /// True while the distinct-value set has stayed within <see cref="ProfileDistinctCap"/>,
        /// meaning <see cref="_frequencies"/> holds every distinct value seen so far.
        /// </summary>
        public bool IsLowCardinality { get; private set; } = true;

        /// <summary>
        /// Exact count while <see cref="IsLowCardinality"/> is true; once the cap overflows this
        /// stays pinned at the cap as an approximate lower bound.
        /// </summary>
        public long DistinctCount { get; private set; }

        /// <summary>Records one observed value (or null) for this column.</summary>
        public void Observe(object? value)
        {
            if (value is null)
                return;

            NonNullCount++;
            if (IsOversized(value))
                return;

            UpdateMinMax(value);

            if (!IsLowCardinality)
                return;

            if (_frequencies.TryGetValue(value, out var count))
            {
                _frequencies[value] = count + 1;
                return;
            }

            if (_frequencies.Count >= ProfileDistinctCap || !budget.TryReserve())
            {
                IsLowCardinality = false;
                return;
            }

            _frequencies[value] = 1;
            DistinctCount = _frequencies.Count;
        }

        /// <summary>
        /// True for text or blob values longer than <c>ProfileMaxTrackedValueLength</c>, which
        /// are never retained. Numeric values are always short enough to keep.
        /// </summary>
        private static bool IsOversized(object value) => value switch
        {
            string text => text.Length > ProfileMaxTrackedValueLength,
            byte[] blob => blob.Length > ProfileMaxTrackedValueLength,
            _ => false,
        };

        /// <summary>
        /// Returns the top <paramref name="count"/> most frequent values observed. Only
        /// meaningful while <see cref="IsLowCardinality"/> is true, since that is the only
        /// case where the full frequency table was retained.
        /// </summary>
        public IReadOnlyList<ValueFrequency> TopValues(int count) =>
            _frequencies
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, Comparer<object>.Create(CompareValues))
                .Take(count)
                .Select(kv => new ValueFrequency(kv.Key, kv.Value))
                .ToList();

        private void UpdateMinMax(object value)
        {
            if (Min is null || CompareValues(value, Min) < 0)
                Min = value;
            if (Max is null || CompareValues(value, Max) > 0)
                Max = value;
        }

        /// <summary>
        /// Best-effort ordering across SQLite's dynamically typed column values: numeric
        /// types compare numerically, everything else falls back to ordinal string
        /// comparison, matching SQLite's own type-then-value collation closely enough
        /// for profiling purposes without pulling in a full affinity-aware comparer.
        /// </summary>
        private static int CompareValues(object a, object b)
        {
            if (a is IComparable comparableA && a.GetType() == b.GetType())
                return comparableA.CompareTo(b);

            if (IsNumeric(a) && IsNumeric(b))
                return Convert.ToDouble(a).CompareTo(Convert.ToDouble(b));

            return string.CompareOrdinal(Convert.ToString(a), Convert.ToString(b));
        }

        private static bool IsNumeric(object value) =>
            value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
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
        ArgumentException.ThrowIfNullOrEmpty(sql);
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
    IReadOnlyList<ColumnProfile> Columns,
    bool IsSampled,
    long RowsScanned);

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