using System;
using Xunit;
using McpSqliteExplorer;
using Microsoft.Data.Sqlite;

namespace McpSqliteExplorer.Tests;

public class IdentifierInjectionTests
{
    [Fact]
    public void TableWithSpaces_IsQuotedCorrectly()
    {
        // Arrange
        var tempDbPath = System.IO.Path.GetTempFileName();
        try
        {
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = tempDbPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString()))
            {
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "CREATE TABLE [table with spaces] (id INTEGER);";
                cmd.ExecuteNonQuery();
            }

            using var explorer = new SqliteExplorer(tempDbPath);

            // Act
            var columns = explorer.DescribeTable("table with spaces");

            // Assert
            Assert.NotNull(columns);
        }
        finally
        {
            if (System.IO.File.Exists(tempDbPath))
                System.IO.File.Delete(tempDbPath);
        }
    }

    [Fact]
    public void TableWithDoubleQuotes_ThrowsException()
    {
        // Arrange
        var tempDbPath = System.IO.Path.GetTempFileName();
        try
        {
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = tempDbPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString()))
            {
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "CREATE TABLE [table\"with\"quotes] (id INTEGER);";
                cmd.ExecuteNonQuery();
            }

            using var explorer = new SqliteExplorer(tempDbPath);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => explorer.DescribeTable("table\"with\"quotes"));
        }
        finally
        {
            if (System.IO.File.Exists(tempDbPath))
                System.IO.File.Delete(tempDbPath);
        }
    }
}
