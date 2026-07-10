using Microsoft.Data.Sqlite;

namespace McpSqliteExplorer.Tests;

/// <summary>
/// Creates a throwaway SQLite file seeded with a tiny schema, and deletes it on
/// dispose. Used as a per-test fixture.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    public string Path { get; }

    public TestDatabase()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"mcp-sqlite-test-{Guid.NewGuid():N}.db");

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString());
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE authors (
                id      INTEGER PRIMARY KEY,
                name    TEXT NOT NULL,
                country TEXT
            );

            CREATE TABLE books (
                id        INTEGER PRIMARY KEY,
                title     TEXT NOT NULL,
                author_id INTEGER NOT NULL REFERENCES authors(id),
                year      INTEGER
            );

            INSERT INTO authors (id, name, country) VALUES
                (1, 'Ursula K. Le Guin', 'US'),
                (2, 'Stanislaw Lem', 'PL'),
                (3, 'Anonymous', NULL);

            INSERT INTO books (id, title, author_id, year) VALUES
                (1, 'The Dispossessed', 1, 1974),
                (2, 'A Wizard of Earthsea', 1, 1968),
                (3, 'Solaris', 2, 1961);

            CREATE VIEW recent_books AS
                SELECT title, year FROM books WHERE year >= 1970;
            """;
        command.ExecuteNonQuery();
    }

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
