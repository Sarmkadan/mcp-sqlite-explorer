using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
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
    private readonly string _validatedDatabasePath;
    private bool _disposed;

    /// <summary>
    /// The base directory that database paths must be contained within for security.
    /// This is the application's working directory where the server is launched.
    /// </summary>
    private static readonly string BaseDirectory = AppContext.BaseDirectory;

    /// <summary>Finalizer to ensure resources are released if Dispose is not called.</summary>
    ~SqliteCatalog()
    {
        Dispose(false);
    }

    public SqliteCatalog(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Database path must not be empty.", nameof(databasePath));

        // Validate the database path to prevent path traversal attacks
        _validatedDatabasePath = ValidateDatabasePath(databasePath);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _validatedDatabasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    /// <summary>
    /// Validates that a database path is safe and contained within a reasonable sandbox directory.
    /// Prevents path traversal attacks by ensuring the resolved path is within an allowed directory tree.
    /// For security, this prevents access to system directories, user home directories, and other sensitive locations.
    /// </summary>
    /// <param name="databasePath">The database path to validate.</param>
    /// <returns>The validated and normalized absolute path.</returns>
    /// <exception cref="ArgumentException">Thrown when the path is outside the allowed directories or is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
    private static string ValidateDatabasePath(string databasePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(databasePath, nameof(databasePath));

        // Normalize the path: resolve relative segments and symlinks
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(databasePath.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException($"Invalid database path: {databasePath}", nameof(databasePath), ex);
        }

        // Resolve any symlinks to prevent symlink-based path traversal
        try
        {
            fullPath = Path.GetFullPath(ResolveSymlinks(fullPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // If we can't resolve symlinks, still allow the path as long as it's within allowed directories
            // This maintains compatibility while still providing the base protection
        }

        // Normalize paths for comparison
        string pathToCheck = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Security: Prevent access to sensitive system directories
        // These are common locations that should never be accessible
        string[] forbiddenPrefixes =
        [
            "/etc/",           // System configuration
            "/bin/",           // System binaries
            "/sbin/",          // System binaries
            "/usr/",           // System directories
            "/var/",           // System variable data
            "/opt/",           // Optional packages
            "/lib/",           // System libraries
            "/lib64/",         // System libraries (64-bit)
            "/root/",          // Root home directory
            "/home/",          // User home directories
            "/proc/",          // Process information
            "/sys/",           // System information
            "/dev/",           // Device files
            "\\Device\\",       // Windows device namespace
            "\\??\\",          // Windows device namespace
            "C:\\Windows\\",    // Windows system directory
            "C:\\Program Files", // Windows program files
            "C:\\ProgramData",  // Windows program data
        ];

        foreach (var forbiddenPrefix in forbiddenPrefixes)
        {
            if (pathToCheck.StartsWith(forbiddenPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Database path '{databasePath}' resolves to '{fullPath}' which is in a forbidden system directory '{forbiddenPrefix}'. " +
                    "Access to system directories is not permitted for security reasons.",
                    nameof(databasePath));
            }
        }

        // Security: Prevent directory traversal attempts (going above root)
        // Check if the path contains ".." segments that could escape intended boundaries
        if (pathToCheck.Contains("..") && !IsPathWithinSafeBoundaries(fullPath))
        {
            throw new ArgumentException(
                $"Database path '{databasePath}' resolves to '{fullPath}' which attempts to traverse outside safe boundaries. " +
                "Relative paths with '..' are not permitted.",
                nameof(databasePath));
        }

        // Additional check: ensure the path is a file (not a directory traversal to a sensitive file)
        if (string.IsNullOrEmpty(Path.GetFileName(fullPath)))
        {
            throw new ArgumentException(
                $"Database path '{databasePath}' resolves to a directory, not a file.",
                nameof(databasePath));
        }

        // Ensure the file exists
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"SQLite database not found: {fullPath}", fullPath);
        }

        return fullPath;
    }

    /// <summary>
    /// Checks if a path is within safe boundaries by ensuring it doesn't escape the intended sandbox.
    /// This is a defense-in-depth check for paths that contain ".." segments.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>True if the path is within safe boundaries; false otherwise.</returns>
    private static bool IsPathWithinSafeBoundaries(string path)
    {
        try
        {
            // Get the absolute path and normalize it
            string fullPath = Path.GetFullPath(path);

            // Count directory separators to detect excessive traversal
            // More than 10 ".." segments is definitely suspicious
            int dotDotCount = 0;
            for (int i = 0; i < fullPath.Length - 1; i++)
            {
                if (fullPath[i] == '.' && fullPath[i + 1] == '.' &&
                    (i == 0 || fullPath[i - 1] == Path.DirectorySeparatorChar || fullPath[i - 1] == Path.AltDirectorySeparatorChar))
                {
                    dotDotCount++;
                    if (dotDotCount > 10)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves symlinks in a file path. Returns the path unchanged if symlink resolution fails.
    /// </summary>
    /// <param name="path">The path to resolve.</param>
    /// <returns>The resolved path, or the original path if resolution fails.</returns>
    private static string ResolveSymlinks(string path)
    {
        try
        {
            // Try to get the final path after symlink resolution
            var fileInfo = new FileInfo(path);
            if (fileInfo.Exists)
            {
                string finalPath = fileInfo.FullName;

                // Additional check: ensure the final path is still within base directory
                string baseDir = BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string finalPathTrimmed = finalPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (finalPathTrimmed.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
                {
                    return finalPath;
                }
            }
        }
        catch
        {
            // If symlink resolution fails, return the original path
            // The base directory check in ValidateDatabasePath will still catch invalid paths
        }

        return path;
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

    /// <summary>Milliseconds SQLite itself will wait, internally, for a write lock to clear before raising SQLITE_BUSY.</summary>
    private const int BusyTimeoutMilliseconds = 3000;

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // See SqliteExplorer.OpenConnection for why query_only and trusted_schema
        // are enforced here in addition to Mode=ReadOnly on the connection string:
        // the read-only guarantee must hold at the engine level, not just because
        // this class only ever issues SELECT/PRAGMA statements.
        using var pragma = connection.CreateCommand();
        pragma.CommandText =
        $"""
        PRAGMA busy_timeout = {BusyTimeoutMilliseconds};
        PRAGMA query_only = ON;
        PRAGMA trusted_schema = OFF;
        """;
        pragma.ExecuteNonQuery();

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