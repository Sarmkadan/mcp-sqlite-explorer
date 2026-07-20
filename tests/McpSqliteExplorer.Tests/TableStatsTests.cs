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
                var dict = stats.ColumnStats.ToDictionary(c => c.Name, c => c.NullCount);
                Assert.Equal(0, dict["id"]);      // Primary key, never null.
                Assert.Equal(1, dict["name"]);    // One NULL name.
                Assert.Equal(1, dict["email"]);   // One NULL email.
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
