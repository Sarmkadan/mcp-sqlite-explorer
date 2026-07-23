using System;
using System.Collections.Generic;

namespace McpSqliteExplorer;

/// <summary>
/// Abstraction over SQLite schema catalog access (sqlite_master, PRAGMA table_info,
/// PRAGMA foreign_key_list, PRAGMA index_list, etc.).
/// </summary>
public interface ISqliteCatalog : IDisposable
{
    /// <summary>
    /// Lists user tables and views, skipping SQLite's internal bookkeeping tables.
    /// </summary>
    /// <returns>List of table/view information.</returns>
    IReadOnlyList<TableInfo> GetTables();

    /// <summary>
    /// Returns the column layout for a table or view using <c>PRAGMA table_info</c>.
    /// </summary>
    /// <param name="table">Table or view name.</param>
    /// <returns>List of column information.</returns>
    /// <exception cref="ArgumentException">Thrown when the table does not exist.</exception>
    IReadOnlyList<ColumnInfo> GetColumns(string table);

    /// <summary>
    /// Lists the foreign keys declared on a table using <c>PRAGMA foreign_key_list</c>.
    /// One entry per referencing column (composite keys produce one entry per column pair).
    /// </summary>
    /// <param name="table">Table name.</param>
    /// <returns>List of foreign key information.</returns>
    /// <exception cref="ArgumentException">Thrown when the table does not exist.</exception>
    IReadOnlyList<ForeignKeyInfo> GetForeignKeys(string table);

    /// <summary>
    /// Lists the indexes defined on a table using <c>PRAGMA index_list</c> and
    /// <c>PRAGMA index_info</c>, including implicit indexes SQLite creates for
    /// UNIQUE constraints and primary keys.
    /// </summary>
    /// <param name="table">Table name.</param>
    /// <returns>List of index information.</returns>
    /// <exception cref="ArgumentException">Thrown when the table does not exist.</exception>
    IReadOnlyList<IndexInfo> GetIndexes(string table);

    /// <summary>
    /// Collects every foreign key in the database, table by table.
    /// </summary>
    /// <returns>List of all foreign key relationships.</returns>
    IReadOnlyList<ForeignKeyInfo> GetForeignKeyGraph();

    /// <summary>
    /// Gets the current schema version. Schema data should be invalidated when this changes.
    /// </summary>
    /// <returns>Schema version identifier.</returns>
    string GetSchemaVersion();
}

/// <summary>
/// Decorator that memoizes schema results within a single tool call.
/// Schema is stable within a tool call, so we can cache results to avoid
/// N+1 PRAGMA storms when analyzing databases with hundreds of tables.
/// </summary>
public sealed class MemoizingSqliteCatalog : ISqliteCatalog
{
    private readonly ISqliteCatalog _inner;
    private readonly Dictionary<string, object> _cache = new(StringComparer.OrdinalIgnoreCase);
    private string? _schemaVersion;

    public MemoizingSqliteCatalog(ISqliteCatalog inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public IReadOnlyList<TableInfo> GetTables()
    {
        var cacheKey = $"tables_{GetSchemaVersion()}";
        if (_cache.TryGetValue(cacheKey, out var cached))
            return (IReadOnlyList<TableInfo>)cached;

        var result = _inner.GetTables();
        _cache[cacheKey] = result;
        return result;
    }

    public IReadOnlyList<ColumnInfo> GetColumns(string table)
    {
        ArgumentException.ThrowIfNullOrEmpty(table);
        var cacheKey = $"columns_{GetSchemaVersion()}_{table}";
        if (_cache.TryGetValue(cacheKey, out var cached))
            return (IReadOnlyList<ColumnInfo>)cached;

        var result = _inner.GetColumns(table);
        _cache[cacheKey] = result;
        return result;
    }

    public IReadOnlyList<ForeignKeyInfo> GetForeignKeys(string table)
    {
        ArgumentException.ThrowIfNullOrEmpty(table);
        var cacheKey = $"foreign_keys_{GetSchemaVersion()}_{table}";
        if (_cache.TryGetValue(cacheKey, out var cached))
            return (IReadOnlyList<ForeignKeyInfo>)cached;

        var result = _inner.GetForeignKeys(table);
        _cache[cacheKey] = result;
        return result;
    }

    public IReadOnlyList<IndexInfo> GetIndexes(string table)
    {
        ArgumentException.ThrowIfNullOrEmpty(table);
        var cacheKey = $"indexes_{GetSchemaVersion()}_{table}";
        if (_cache.TryGetValue(cacheKey, out var cached))
            return (IReadOnlyList<IndexInfo>)cached;

        var result = _inner.GetIndexes(table);
        _cache[cacheKey] = result;
        return result;
    }

    public IReadOnlyList<ForeignKeyInfo> GetForeignKeyGraph()
    {
        var cacheKey = $"foreign_key_graph_{GetSchemaVersion()}";
        if (_cache.TryGetValue(cacheKey, out var cached))
            return (IReadOnlyList<ForeignKeyInfo>)cached;

        var result = _inner.GetForeignKeyGraph();
        _cache[cacheKey] = result;
        return result;
    }

    public string GetSchemaVersion()
    {
        // Cache schema version to avoid repeated PRAGMA calls
        _schemaVersion ??= _inner.GetSchemaVersion();
        return _schemaVersion;
    }

    public void Dispose()
    {
        _inner.Dispose();
        _cache.Clear();
        GC.SuppressFinalize(this);
    }
}