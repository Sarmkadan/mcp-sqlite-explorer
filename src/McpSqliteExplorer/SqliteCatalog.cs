using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace McpSqliteExplorer;

/// <summary>
/// Concrete implementation of <see cref="ISqliteCatalog"/> that provides schema access
/// to a SQLite database file. This class centralizes all PRAGMA and sqlite_master
/// queries that were previously duplicated across SqliteExplorer and SqliteAnalysisTools.
/// </summary>
internal sealed class SqliteCatalog : ISqliteCatalog
{
    private readonly string _connectionString;
    private bool _disposed;

    /// <summary>Finalizer to ensure resources are released if Dispose is not called.</summary>
    ~SqliteCatalog()
    {
        Dispose(false);
    }

    public SqliteCatalog(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Database path must not be empty.", nameof(databasePath));

        if (!File.Exists(databasePath))
            throw new FileNotFoundException($"SQLite database not found: {databasePath}", databasePath);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    public IReadOnlyList<TableInfo> GetTables()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT name, type
            FROM sqlite_master
            WHERE type IN ('table', 'view')
            AND name NOT LIKE 'sqlite_%'
            ORDER BY type, name;
            """;

        var tables = new List<TableInfo>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            tables.Add(new TableInfo(reader.GetString(0), reader.GetString(1)));

        return tables;
    }

    public IReadOnlyList<ColumnInfo> GetColumns(string table)
    {
        var safeName = RequireExistingTable(table);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        // PRAGMA does not accept bound parameters for the identifier, so the name
        // is validated against sqlite_master first and quoted here.
        command.CommandText = $"PRAGMA table_info({QuoteIdentifier(safeName)});";

        var columns = new List<ColumnInfo>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(new ColumnInfo(
                Name: reader.GetString(reader.GetOrdinal("name")),
                Type: reader.IsDBNull(reader.GetOrdinal("type")) ? "" : reader.GetString(reader.GetOrdinal("type")),
                NotNull: reader.GetInt64(reader.GetOrdinal("notnull")) != 0,
                DefaultValue: reader.IsDBNull(reader.GetOrdinal("dflt_value")) ? null : reader.GetString(reader.GetOrdinal("dflt_value")),
                PrimaryKey: reader.GetInt64(reader.GetOrdinal("pk")) != 0
            ));
        }

        return columns;
    }

    public IReadOnlyList<ForeignKeyInfo> GetForeignKeys(string table)
    {
        var safeName = RequireExistingTable(table);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA foreign_key_list({QuoteIdentifier(safeName)});";

        var foreignKeys = new List<ForeignKeyInfo>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var toOrdinal = reader.GetOrdinal("to");
            foreignKeys.Add(new ForeignKeyInfo(
                Table: safeName,
                Column: reader.GetString(reader.GetOrdinal("from")),
                ReferencesTable: reader.GetString(reader.GetOrdinal("table")),
                // NULL means "the referenced table's primary key".
                ReferencesColumn: reader.IsDBNull(toOrdinal) ? null : reader.GetString(toOrdinal),
                OnUpdate: reader.GetString(reader.GetOrdinal("on_update")),
                OnDelete: reader.GetString(reader.GetOrdinal("on_delete"))
            ));
        }

        return foreignKeys;
    }

    public IReadOnlyList<IndexInfo> GetIndexes(string table)
    {
        var safeName = RequireExistingTable(table);

        using var connection = OpenConnection();
        var indexes = new List<IndexInfo>();

        // First, get the list of indexes
        using (var listCommand = connection.CreateCommand())
        {
            listCommand.CommandText = $"PRAGMA index_list({QuoteIdentifier(safeName)});";
            using var reader = listCommand.ExecuteReader();
            while (reader.Read())
            {
                indexes.Add(new IndexInfo(
                    Name: reader.GetString(reader.GetOrdinal("name")),
                    Table: safeName,
                    Unique: reader.GetInt64(reader.GetOrdinal("unique")) != 0,
                    // 'c' = CREATE INDEX, 'u' = UNIQUE constraint, 'pk' = primary key
                    Origin: reader.GetString(reader.GetOrdinal("origin")) switch
                    {
                        "c" => "create-index",
                        "u" => "unique-constraint",
                        "pk" => "primary-key",
                        var other => other,
                    },
                    Partial: reader.GetInt64(reader.GetOrdinal("partial")) != 0,
                    Columns: []
                ));
            }
        }

        // Then, for each index, get the indexed columns
        for (var i = 0; i < indexes.Count; i++)
        {
            using var infoCommand = connection.CreateCommand();
            infoCommand.CommandText = $"PRAGMA index_info({QuoteIdentifier(indexes[i].Name)});";

            var columns = new List<string>();
            using var reader = infoCommand.ExecuteReader();
            while (reader.Read())
            {
                var nameOrdinal = reader.GetOrdinal("name");
                // Expression indexes report NULL for the column name.
                columns.Add(reader.IsDBNull(nameOrdinal) ? "<expression>" : reader.GetString(nameOrdinal));
            }

            indexes[i] = indexes[i] with { Columns = columns };
        }

        return indexes;
    }

    public IReadOnlyList<ForeignKeyInfo> GetForeignKeyGraph()
    {
        var edges = new List<ForeignKeyInfo>();
        foreach (var table in GetTables())
        {
            if (table.Type != "table")
                continue;
            edges.AddRange(GetForeignKeys(table.Name));
        }

        return edges;
    }

    public string GetSchemaVersion()
    {
        // Use the schema_version PRAGMA to detect schema changes
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA schema_version;";
        return command.ExecuteScalar()?.ToString() ?? "0";
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Dispose managed resources
        }

        // No unmanaged resources to dispose
        _disposed = true;
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private string RequireExistingTable(string table)
    {
        if (string.IsNullOrWhiteSpace(table))
            throw new ArgumentException("Table name must not be empty.", nameof(table));

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT name FROM sqlite_master
            WHERE type IN ('table', 'view') AND name = $name
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$name", table);

        var resolved = command.ExecuteScalar() as string;
        if (resolved is null)
            throw new ArgumentException($"No such table or view: '{table}'.", nameof(table));

        return resolved;
    }

    private static string QuoteIdentifier(string identifier) =>
        '"' + identifier.Replace("\"", "\"\"") + '"';
}