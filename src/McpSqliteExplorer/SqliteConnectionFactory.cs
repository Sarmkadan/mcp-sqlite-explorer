namespace McpSqliteExplorer;

/// <summary>
/// Creates a <see cref="Microsoft.Data.Sqlite.SqliteConnection"/> for a SQLite database file.
/// The connection is configured in read-only mode, preventing any write operations.
/// Validates that the database file exists and has a valid .db extension.
/// </summary>
public sealed class SqliteConnectionFactory
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteConnectionFactory"/> class.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite database file. Must exist and have a .db extension.</param>
    /// <exception cref="ArgumentException">Thrown when the database file does not exist or has an invalid extension.</exception>
    public SqliteConnectionFactory(string databasePath)
    {
        if (!System.IO.File.Exists(databasePath))
        {
            throw new ArgumentException("Database file does not exist.", nameof(databasePath));
        }
        if (!databasePath.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Database file must be a SQLite file.", nameof(databasePath));
        }
    }

    /// <summary>
    /// Opens a read-only <see cref="Microsoft.Data.Sqlite.SqliteConnection"/> for the specified database path.
    /// The connection uses <c>Mode=ReadOnly</c> and has <c>query_only</c> and <c>trusted_schema</c> set at the engine level.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite database file.</param>
    /// <returns>An open <see cref="Microsoft.Data.Sqlite.SqliteConnection"/> that cannot modify the database file.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="databasePath"/> is null or empty.</exception>
    /// <exception cref="Microsoft.Data.Sqlite.SqliteException">Thrown when the database cannot be opened or a pragma is rejected.</exception>
    public Microsoft.Data.Sqlite.SqliteConnection CreateConnection(string databasePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(databasePath);

        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly,
        };

        return SqliteReadOnlyConnection.Open(builder.ToString());
    }
}