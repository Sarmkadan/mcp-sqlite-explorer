using Microsoft.Data.Sqlite;

namespace McpSqliteExplorer;

/// <summary>
/// Opens SQLite connections with the engine-level read-only settings that every
/// component of this server relies on. The text-level checks in
/// <see cref="SqliteExplorer"/> only produce friendly errors; the guarantee that
/// nothing modifies the database lives here.
/// </summary>
internal static class SqliteReadOnlyConnection
{
    /// <summary>Milliseconds SQLite itself will wait, internally, for a write lock to clear before raising SQLITE_BUSY.</summary>
    internal const int BusyTimeoutMilliseconds = 3000;

    /// <summary>
    /// Opens a connection and applies the engine pragmas before returning it.
    /// <c>Mode=ReadOnly</c> in the connection string keeps SQLite from taking a write
    /// lock on the file. <c>query_only = ON</c> makes the engine reject writing
    /// statements up front, including writes to an ATTACHed database.
    /// <c>trusted_schema = OFF</c> stops schema-embedded expressions from calling
    /// functions that are not known to be safe. <c>busy_timeout</c> lets SQLite
    /// wait out a short writer before reporting SQLITE_BUSY.
    /// </summary>
    /// <param name="connectionString">A connection string built with <c>Mode=ReadOnly</c>.</param>
    /// <returns>An open <see cref="SqliteConnection"/> that cannot modify the database file.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="connectionString"/> is null or empty.</exception>
    /// <exception cref="SqliteException">Thrown when the database cannot be opened or a pragma is rejected.</exception>
    internal static SqliteConnection Open(string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        var connection = new SqliteConnection(connectionString);
        try
        {
            connection.Open();

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
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
