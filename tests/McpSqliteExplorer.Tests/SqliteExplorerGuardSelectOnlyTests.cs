using System;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using McpSqliteExplorer;
using Xunit;

namespace McpSqliteExplorer.Tests;

public class SqliteExplorerGuardSelectOnlyTests
{
    private string CreateTestDatabase()
    {
        var dbFile = Path.GetTempFileName();
        File.Delete(dbFile);
        var connection = new SqliteConnection($"Data Source={dbFile}");
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE Test (Id INTEGER PRIMARY KEY, Name TEXT);";
        command.ExecuteNonQuery();
        connection.Close();
        return dbFile;
    }

    [Fact]
    public void RunSelect_ValidSelectStatement_DoesNotThrow()
    {
        string dbFile = CreateTestDatabase();
        try
        {
            var explorer = new SqliteExplorer(dbFile);
            // Should not throw
            var result = explorer.RunSelect("SELECT * FROM Test");
            Assert.NotNull(result);
        }
        finally
        {
            File.Delete(dbFile);
        }
    }

    [Fact]
    public void RunSelect_SelectWithCte_DoesNotThrow()
    {
        string dbFile = CreateTestDatabase();
        try
        {
            var explorer = new SqliteExplorer(dbFile);
            // Should not throw
            var result = explorer.RunSelect("WITH cte AS (SELECT 1 AS x) SELECT * FROM cte");
            Assert.NotNull(result);
            Assert.Single(result.Columns);
            Assert.Equal("x", result.Columns[0]);
            Assert.Single(result.Rows);
            Assert.Equal(1L, result.Rows[0][0]);
        }
        finally
        {
            File.Delete(dbFile);
        }
    }

    [Fact]
    public void RunSelect_InsertStatement_ThrowsArgumentException()
    {
        string dbFile = CreateTestDatabase();
        try
        {
            var explorer = new SqliteExplorer(dbFile);
            Assert.Throws<ArgumentException>(() => explorer.RunSelect("INSERT INTO Test (Name) VALUES ('test')"));
        }
        finally
        {
            File.Delete(dbFile);
        }
    }

    [Fact]
    public void RunSelect_UpdateStatement_ThrowsArgumentException()
    {
        string dbFile = CreateTestDatabase();
        try
        {
            var explorer = new SqliteExplorer(dbFile);
            Assert.Throws<ArgumentException>(() => explorer.RunSelect("UPDATE Test SET Name = 'updated' WHERE Id = 1"));
        }
        finally
        {
            File.Delete(dbFile);
        }
    }

    [Fact]
    public void RunSelect_DeleteStatement_ThrowsArgumentException()
    {
        string dbFile = CreateTestDatabase();
        try
        {
            var explorer = new SqliteExplorer(dbFile);
            Assert.Throws<ArgumentException>(() => explorer.RunSelect("DELETE FROM Test WHERE Id = 1"));
        }
        finally
        {
            File.Delete(dbFile);
        }
    }

    [Fact]
    public void RunSelect_DropStatement_ThrowsArgumentException()
    {
        string dbFile = CreateTestDatabase();
        try
        {
            var explorer = new SqliteExplorer(dbFile);
            Assert.Throws<ArgumentException>(() => explorer.RunSelect("DROP TABLE Test"));
        }
        finally
        {
            File.Delete(dbFile);
        }
    }

    [Fact]
    public void RunSelect_MultipleSelectStatements_ThrowsSqliteException()
    {
        string dbFile = CreateTestDatabase();
        try
        {
            var explorer = new SqliteExplorer(dbFile);
            Assert.Throws<SqliteException>(() => explorer.RunSelect("SELECT * FROM Test; SELECT * FROM Test"));
        }
        finally
        {
            File.Delete(dbFile);
        }
    }

    [Fact]
    public void RunSelect_SelectThenInsert_ThrowsArgumentException()
    {
        string dbFile = CreateTestDatabase();
        try
        {
            var explorer = new SqliteExplorer(dbFile);
            Assert.Throws<ArgumentException>(() => explorer.RunSelect("SELECT * FROM Test; INSERT INTO Test (Name) VALUES ('test')"));
        }
        finally
        {
            File.Delete(dbFile);
        }
    }

    [Fact]
    public void RunSelect_InsertLowercase_ThrowsArgumentException()
    {
        string dbFile = CreateTestDatabase();
        try
        {
            var explorer = new SqliteExplorer(dbFile);
            Assert.Throws<ArgumentException>(() => explorer.RunSelect("insert into test (name) values ('test')"));
        }
        finally
        {
            File.Delete(dbFile);
        }
    }
}