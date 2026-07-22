using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace McpSqliteExplorer.Tests
{
    /// <summary>
    /// Provides unit tests for SQL formatting functionality in the McpSqliteExplorer library.
    /// These tests verify that SQL query formatting and table inspection tools produce correct output.
    /// </summary>
    public sealed class SqliteToolsFormattingTests : IDisposable
    {
        /// <summary>
        /// Test database instance used for all test cases.
        /// </summary>
        private readonly TestDatabase _testDatabase;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqliteToolsFormattingTests"/> class.
        /// Creates a new test database instance for use in all test methods.
        /// </summary>
        public SqliteToolsFormattingTests()
        {
            _testDatabase = new TestDatabase();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// Disposes the test database instance used by this test class.
        /// </summary>
        public void Dispose()
        {
            _testDatabase.Dispose();
        }

        [Fact]
        /// <summary>
        /// Tests that listing tables from an empty database returns an empty collection.
        /// </summary>
        public void ListTables_EmptyDatabase_ReturnsEmptyList()
        {
            // Arrange
            var explorer = new SqliteExplorer(_testDatabase.Path);

            // Act
            var result = SqliteTools.ListTables(explorer);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        /// <summary>
        /// Tests that describing a table with all column types returns a description containing all column names.
        /// </summary>
        public void DescribeTable_TableWithAllColumnTypes_ReturnsCorrectDescription()
        {
            // Arrange
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = _testDatabase.Path,
                Mode = SqliteOpenMode.ReadWriteCreate,
            }.ToString());
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
            """
CREATE TABLE test_table (
id INTEGER PRIMARY KEY,
name TEXT NOT NULL,
country TEXT,
flag BOOLEAN NOT NULL DEFAULT FALSE,
number REAL NOT NULL DEFAULT 0.0,
date DATE NOT NULL DEFAULT '2022-01-01',
time TIME NOT NULL DEFAULT '12:00:00',
datetime DATETIME NOT NULL DEFAULT '2022-01-01 12:00:00'
);
""";
            command.ExecuteNonQuery();

            var explorer = new SqliteExplorer(_testDatabase.Path);

            // Act
            var result = SqliteTools.DescribeTable(explorer, "test_table");

            // Assert
            Assert.Contains("id", result);
            Assert.Contains("name", result);
            Assert.Contains("country", result);
            Assert.Contains("flag", result);
            Assert.Contains("number", result);
            Assert.Contains("date", result);
            Assert.Contains("time", result);
            Assert.Contains("datetime", result);
        }

        [Fact]
        /// <summary>
        /// Tests that sampling rows from a table enforces the specified row limit.
        /// </summary>
        public void SampleRows_TableWithRows_RowCapEnforced()
        {
            // Arrange
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = _testDatabase.Path,
                Mode = SqliteOpenMode.ReadWriteCreate,
            }.ToString());
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
            """
CREATE TABLE test_table (
id INTEGER PRIMARY KEY,
name TEXT NOT NULL
);

INSERT INTO test_table (id, name) VALUES
(1, 'John'),
(2, 'Jane'),
(3, 'Bob'),
(4, 'Alice'),
(5, 'Charlie');
""";
            command.ExecuteNonQuery();

            var explorer = new SqliteExplorer(_testDatabase.Path);

            // Act
            var result = SqliteTools.SampleRows(explorer, "test_table", 3);

            // Assert
            Assert.True(result.Length <= 3);
        }
    }
}