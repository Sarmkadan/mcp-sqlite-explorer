using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Xunit;
using McpSqliteExplorer;

namespace McpSqliteExplorer.Tests
{
    public class TableStatsTests
    {
        [Fact]
        public void TableStats_ReturnsCorrectCounts()
        {
            // Create a temporary SQLite file and populate it with test data.
            var tempPath = Path.GetTempFileName();
            try
            {
                using (var conn = new SqliteConnection($"Data Source={tempPath}"))
                {
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                    CREATE TABLE users (
                        id INTEGER PRIMARY KEY,
                        name TEXT,
                        email TEXT
                    );
                    INSERT INTO users (name, email) VALUES ('Alice', 'alice@example.com');
                    INSERT INTO users (name, email) VALUES (NULL, 'bob@example.com');
                    INSERT INTO users (name, email) VALUES ('Charlie', NULL);
                    ";
                    cmd.ExecuteNonQuery();
                }

                var explorer = new SqliteExplorer(tempPath);
                var stats = explorer.TableStats("users");

                // Verify total row count.
                Assert.Equal(3, stats.RowCount);

                // Verify per‑column NULL counts (only the first 20 columns are reported).
                var dict = stats.Columns.ToDictionary(c => c.Name, c => c.NullCount);
                Assert.Equal(0, dict["id"]); // Primary key, never null.
                Assert.Equal(1, dict["name"]); // One NULL name.
                Assert.Equal(1, dict["email"]); // One NULL email.
            }
            finally
            {
                // Clean up the temporary file.
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Fact]
        public void TableStats_RowCountUsesLongType()
        {
            // This test ensures that TableStats uses long for RowCount to avoid overflow
            // with tables containing more than 2.1 billion rows.
            var tempPath = Path.GetTempFileName();
            try
            {
                using (var conn = new SqliteConnection($"Data Source={tempPath}"))
                {
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "CREATE TABLE large (id INTEGER);";
                    cmd.ExecuteNonQuery();
                }

                var explorer = new SqliteExplorer(tempPath);
                var stats = explorer.TableStats("large");

                // RowCount should be of type long (Int64)
                Assert.IsType<long>(stats.RowCount);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Fact]
        public void TableStats_ColumnNullCountUsesLongType()
        {
            // This test ensures that ColumnProfile.NullCount uses long to avoid overflow
            // with columns containing more than 2.1 billion NULL values.
            var tempPath = Path.GetTempFileName();
            try
            {
                using (var conn = new SqliteConnection($"Data Source={tempPath}"))
                {
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                    CREATE TABLE big_table (
                        id INTEGER PRIMARY KEY,
                        data TEXT
                    );
                    " +
                    string.Join("\n", Enumerable.Range(1, 10000).Select(i =>
                        "INSERT INTO big_table (data) VALUES (NULL);"));
                    cmd.ExecuteNonQuery();
                }

                var explorer = new SqliteExplorer(tempPath);
                var stats = explorer.TableStats("big_table");

                // NullCount should be of type long (Int64)
                var columnWithNulls = stats.Columns.FirstOrDefault(c => c.NullCount > 0);
                if (columnWithNulls != null)
                {
                    Assert.IsType<long>(columnWithNulls.NullCount);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Fact]
        public void TableStats_AndProfileTable_ReportIdenticalRowCounts()
        {
            // Create a temporary SQLite file and populate it with test data.
            var tempPath = Path.GetTempFileName();
            try
            {
                using (var conn = new SqliteConnection($"Data Source={tempPath}"))
                {
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                    CREATE TABLE test_table (
                        id INTEGER PRIMARY KEY,
                        name TEXT,
                        value INTEGER
                    );
                    INSERT INTO test_table (name, value) VALUES ('A', 1);
                    INSERT INTO test_table (name, value) VALUES ('B', 2);
                    INSERT INTO test_table (name, value) VALUES ('C', 3);
                    ";
                    cmd.ExecuteNonQuery();
                }

                var explorer = new SqliteExplorer(tempPath);

                // Get row count from TableStats (basic stats)
                var basicStats = explorer.TableStats("test_table");

                // Get row count from ProfileTable (detailed profiling)
                var profile = explorer.ProfileTable("test_table");

                // Both should report the same row count
                Assert.Equal(profile.RowCount, basicStats.RowCount);
            }
            finally
            {
                // Clean up the temporary file.
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }
}
