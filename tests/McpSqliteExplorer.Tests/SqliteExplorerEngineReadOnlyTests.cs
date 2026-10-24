using System.Text.Json;
using Microsoft.Data.Sqlite;
using Xunit;

namespace McpSqliteExplorer.Tests;

/// <summary>
/// Verifies that the read-only guarantee is enforced by the SQLite engine itself
/// (<c>Mode=ReadOnly</c> plus <c>PRAGMA query_only = ON</c>), not only by the text
/// guard in <see cref="SqliteExplorer"/>. The engine tests run statements directly
/// on the connection the explorer opens, which skips the text guard, so the
/// <see cref="SqliteException"/> they assert on comes from SQLite itself. The tool
/// tests go through the raw <c>run_select</c> entry point and check that the call
/// fails and leaves the file byte-for-byte unchanged.
/// </summary>
public sealed class SqliteExplorerEngineReadOnlyTests : IDisposable
{
    private readonly TestDatabase _database;

    public SqliteExplorerEngineReadOnlyTests() => _database = new TestDatabase();

    public void Dispose() => _database.Dispose();

    [Fact]
    public void OpenConnection_AppliesEngineReadOnlyPragmas()
    {
        using var explorer = new SqliteExplorer(_database.Path);
        using var connection = explorer.OpenConnection();

        Assert.Equal(1L, ReadScalar(connection, "PRAGMA query_only;"));
        Assert.Equal(0L, ReadScalar(connection, "PRAGMA trusted_schema;"));
    }

    [Theory]
    [InlineData("INSERT INTO authors (id, name) VALUES (99, 'x')")]
    [InlineData("UPDATE authors SET name = 'x' WHERE id = 1")]
    [InlineData("DELETE FROM authors WHERE id = 1")]
    [InlineData("DROP TABLE loans")]
    [InlineData("CREATE TABLE evil (id INTEGER)")]
    [InlineData("WITH src AS (SELECT 99 AS id) INSERT INTO authors (id, name) SELECT id, 'x' FROM src")]
    public void OpenConnection_WriteStatement_FailsWithSqliteReadOnly(string sql)
    {
        var bytesBefore = File.ReadAllBytes(_database.Path);
        using var explorer = new SqliteExplorer(_database.Path);

        var ex = ExecuteExpectingFailure(explorer, sql);

        Assert.Equal(SQLitePCL.raw.SQLITE_READONLY, ex.SqliteErrorCode);
        Assert.Equal(bytesBefore, File.ReadAllBytes(_database.Path));
    }

    [Fact]
    public void OpenConnection_UpdateSchemaAfterPragmaWritableSchema_FailsWithSqliteReadOnly()
    {
        var bytesBefore = File.ReadAllBytes(_database.Path);
        using var explorer = new SqliteExplorer(_database.Path);
        using var connection = explorer.OpenConnection();

        ExecuteNonQuery(connection, "PRAGMA writable_schema = ON;");
        var ex = Assert.Throws<SqliteException>(() =>
            ExecuteNonQuery(connection, "UPDATE sqlite_master SET sql = sql WHERE name = 'authors';"));

        Assert.Equal(SQLitePCL.raw.SQLITE_READONLY, ex.SqliteErrorCode);
        Assert.Equal(bytesBefore, File.ReadAllBytes(_database.Path));
    }

    [Fact]
    public void OpenConnection_AttachNewFile_FailsWithoutCreatingIt()
    {
        var bytesBefore = File.ReadAllBytes(_database.Path);
        var attachTarget = Path.Combine(Path.GetTempPath(), $"mcp-sqlite-attach-{Guid.NewGuid():N}.db");
        using var explorer = new SqliteExplorer(_database.Path);

        try
        {
            // A read-only connection cannot create a new file, so SQLite reports CANTOPEN.
            var ex = ExecuteExpectingFailure(explorer, $"ATTACH DATABASE '{attachTarget}' AS evil;");

            Assert.Equal(SQLitePCL.raw.SQLITE_CANTOPEN, ex.SqliteErrorCode);
            Assert.False(File.Exists(attachTarget), "ATTACH must not create a new database file.");
        }
        finally
        {
            if (File.Exists(attachTarget))
                File.Delete(attachTarget);
        }

        Assert.Equal(bytesBefore, File.ReadAllBytes(_database.Path));
    }

    [Fact]
    public void OpenConnection_WriteToAttachedDatabase_FailsAndLeavesAttachedFileUnchanged()
    {
        using var other = new TestDatabase();
        var otherBytesBefore = File.ReadAllBytes(other.Path);
        using var explorer = new SqliteExplorer(_database.Path);
        using var connection = explorer.OpenConnection();

        ExecuteNonQuery(connection, $"ATTACH DATABASE '{other.Path}' AS evil;");
        var ex = Assert.Throws<SqliteException>(() =>
            ExecuteNonQuery(connection, "INSERT INTO evil.authors (id, name) VALUES (99, 'x');"));

        Assert.Equal(SQLitePCL.raw.SQLITE_READONLY, ex.SqliteErrorCode);
        Assert.Equal(otherBytesBefore, File.ReadAllBytes(other.Path));
    }

    [Fact]
    public void SqliteConnectionFactory_CreateConnection_EnforcesEngineReadOnly()
    {
        var bytesBefore = File.ReadAllBytes(_database.Path);
        var factory = new SqliteConnectionFactory(_database.Path);
        using var connection = factory.CreateConnection(_database.Path);

        Assert.Equal(1L, ReadScalar(connection, "PRAGMA query_only;"));

        var ex = ExecuteNonQueryExpectingFailure(connection, "DELETE FROM authors WHERE id = 1;");

        Assert.Equal(SQLitePCL.raw.SQLITE_READONLY, ex.SqliteErrorCode);
        Assert.Equal(bytesBefore, File.ReadAllBytes(_database.Path));
    }

    [Theory]
    [InlineData("INSERT INTO authors (id, name) VALUES (99, 'x')")]
    [InlineData("UPDATE authors SET name = 'x' WHERE id = 1")]
    [InlineData("WITH src AS (SELECT 99 AS id) INSERT INTO authors (id, name) SELECT id, 'x' FROM src")]
    [InlineData("SELECT 1; DELETE FROM authors")]
    [InlineData("PRAGMA writable_schema = ON")]
    [InlineData("ATTACH DATABASE '/nonexistent/mcp-sqlite-attach.db' AS evil")]
    public void RunSelectTool_WriteAttempt_ReturnsErrorAndLeavesFileUnchanged(string sql)
    {
        var bytesBefore = File.ReadAllBytes(_database.Path);
        using var explorer = new SqliteExplorer(_database.Path);

        var json = SqliteTools.RunSelect(explorer, sql);

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
        Assert.Equal(bytesBefore, File.ReadAllBytes(_database.Path));
    }

    private static SqliteException ExecuteExpectingFailure(SqliteExplorer explorer, string sql)
    {
        using var connection = explorer.OpenConnection();
        return ExecuteNonQueryExpectingFailure(connection, sql);
    }

    private static SqliteException ExecuteNonQueryExpectingFailure(SqliteConnection connection, string sql) =>
        Assert.Throws<SqliteException>(() => ExecuteNonQuery(connection, sql));

    private static void ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static long ReadScalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }
}
