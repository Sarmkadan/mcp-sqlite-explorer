using Microsoft.Data.Sqlite;

namespace McpSqliteExplorer.Tests;

/// <summary>
/// Creates a throwaway SQLite file with the given tables, each holding the given number of rows, and
/// deletes it on dispose. Use it when a test needs particular table names or row counts; use
/// <see cref="TestDatabase"/> for the shared fixture schema.
/// </summary>
public sealed class TempDatabaseWithTables : IDisposable
{
    /// <summary>Full path of the database file.</summary>
    public string Path { get; }

    /// <summary>
    /// Creates the file and seeds each table.
    /// </summary>
    /// <param name="tables">Table name and number of rows to insert into it.</param>
    public TempDatabaseWithTables(params (string Table, int Rows)[] tables)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mcp-sqlite-summary-{Guid.NewGuid():N}.db");

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString());
        connection.Open();

        foreach (var (table, rows) in tables)
        {
            using var create = connection.CreateCommand();
            create.CommandText = $"CREATE TABLE \"{table}\" (id INTEGER PRIMARY KEY, value TEXT);";
            create.ExecuteNonQuery();

            for (var i = 1; i <= rows; i++)
            {
                using var insert = connection.CreateCommand();
                insert.CommandText = $"INSERT INTO \"{table}\" (id, value) VALUES ($id, $value);";
                insert.Parameters.AddWithValue("$id", i);
                insert.Parameters.AddWithValue("$value", $"row {i}");
                insert.ExecuteNonQuery();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(Path))
                File.Delete(Path);
        }
        catch (IOException)
        {
            // Best-effort cleanup; a leftover temp file is harmless.
        }
    }
}
