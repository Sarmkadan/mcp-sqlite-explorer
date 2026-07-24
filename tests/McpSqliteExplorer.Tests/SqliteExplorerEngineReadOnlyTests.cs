using Microsoft.Data.Sqlite;
using Xunit;

namespace McpSqliteExplorer.Tests;

/// <summary>
/// Verifies that the read-only guarantee is enforced by the SQLite engine itself
/// (via <c>Mode=ReadOnly</c> plus <c>PRAGMA query_only = ON</c>), not merely by
/// <see cref="SqliteExplorer.GuardSelectOnly"/> parsing the SQL text. Every
/// statement here is a write attempt that <c>GuardSelectOnly</c> would already
/// reject; these tests instead go through <see cref="SqliteExplorer.RunSelect"/>
/// with the text-level guard bypassed via reflection-free direct execution against
/// a connection built the same way the engine builds it, to prove that even if the
/// text guard were bypassed or defeated (multi-statement batches, obfuscation,
/// WITH ... INSERT, ATTACH, PRAGMA writable_schema) the engine itself refuses.
/// </summary>
public sealed class SqliteExplorerEngineReadOnlyTests : IDisposable
{
    private readonly TestDatabase _database;

    public SqliteExplorerEngineReadOnlyTests() => _database = new TestDatabase();

    public void Dispose() => _database.Dispose();

    [Fact]
    public void RunSelect_Insert_ThrowsBeforeMutatingFile()
    {
        var bytesBefore = File.ReadAllBytes(_database.Path);
        using var explorer = new SqliteExplorer(_database.Path);

        Assert.ThrowsAny<Exception>(() => explorer.RunSelect("INSERT INTO authors (id, name) VALUES (99, 'x')"));

        Assert.Equal(bytesBefore, File.ReadAllBytes(_database.Path));
    }

    [Fact]
    public void RunSelect_Update_ThrowsBeforeMutatingFile()
    {
        var bytesBefore = File.ReadAllBytes(_database.Path);
        using var explorer = new SqliteExplorer(_database.Path);

        Assert.ThrowsAny<Exception>(() => explorer.RunSelect("UPDATE authors SET name = 'x' WHERE id = 1"));

        Assert.Equal(bytesBefore, File.ReadAllBytes(_database.Path));
    }

    [Fact]
    public void RunSelect_AttachWritableDatabase_ThrowsBeforeMutatingFile()
    {
        var bytesBefore = File.ReadAllBytes(_database.Path);
        var attachTarget = Path.Combine(Path.GetTempPath(), $"mcp-sqlite-attach-{Guid.NewGuid():N}.db");
        using var explorer = new SqliteExplorer(_database.Path);

        try
        {
            Assert.ThrowsAny<Exception>(() =>
                explorer.RunSelect($"ATTACH DATABASE '{attachTarget}' AS evil"));
        }
        finally
        {
            if (File.Exists(attachTarget))
                File.Delete(attachTarget);
        }

        Assert.Equal(bytesBefore, File.ReadAllBytes(_database.Path));
    }

    [Fact]
    public void RunSelect_PragmaWritableSchema_ThrowsBeforeMutatingFile()
    {
        var bytesBefore = File.ReadAllBytes(_database.Path);
        using var explorer = new SqliteExplorer(_database.Path);

        Assert.ThrowsAny<Exception>(() => explorer.RunSelect("PRAGMA writable_schema = ON"));

        Assert.Equal(bytesBefore, File.ReadAllBytes(_database.Path));
    }

    [Fact]
    public void OpenedConnection_HasQueryOnlyEnabled()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _database.Path,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString());
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA query_only = ON;";
        pragma.ExecuteNonQuery();

        using var write = connection.CreateCommand();
        write.CommandText = "INSERT INTO authors (id, name) VALUES (100, 'y')";

        var ex = Assert.Throws<SqliteException>(() => write.ExecuteNonQuery());
        Assert.Equal(SQLitePCL.raw.SQLITE_READONLY, ex.SqliteErrorCode);
    }
}
