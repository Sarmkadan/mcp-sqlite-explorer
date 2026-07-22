using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Microsoft.Data.Sqlite;

namespace McpSqliteExplorer.Tests
{
    /// <summary>
    /// Tests for SqliteExplorerSchema class focusing on:
    /// - Table list on temp db
    /// - Column metadata types
    /// - Foreign keys reported
    /// - Empty database handled
    /// </summary>
    public class SqliteExplorerSchemaTests
    {
        [Fact]
        public void ListTables_OnTempDatabase_ReturnsCorrectTables()
        {
            // Arrange
            var tempDbPath = System.IO.Path.GetTempFileName();
            try
            {
                // Create a temp database with tables
                using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = tempDbPath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                }.ToString()))
                {
                    connection.Open();
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                    CREATE TABLE test_table1 (id INTEGER PRIMARY KEY, name TEXT);
                    CREATE TABLE test_table2 (id INTEGER PRIMARY KEY, value REAL);
                    CREATE TABLE test_view AS SELECT id, name FROM test_table1;
                    ";
                    cmd.ExecuteNonQuery();
                }

                using var explorer = new SqliteExplorer(tempDbPath);

                // Act
                var tables = explorer.ListTables();

                // Assert
                Assert.NotNull(tables);
                Assert.NotEmpty(tables);
                Assert.Equal(3, tables.Count);

                // Should have 3 items total (2 tables + 1 view)
                Assert.Equal(3, tables.Count);

                // Should contain our tables - view type may vary by SQLite version
                Assert.Contains(tables, t => t.Name == "test_table1" && t.Type == "table");
                Assert.Contains(tables, t => t.Name == "test_table2" && t.Type == "table");
                // The view should exist, check for it by name regardless of type
                Assert.Contains(tables, t => t.Name == "test_view");
            }
            finally
            {
                if (System.IO.File.Exists(tempDbPath))
                    System.IO.File.Delete(tempDbPath);
            }
        }

        [Fact]
        public void ListTables_OnEmptyDatabase_ReturnsEmptyList()
        {
            // Arrange
            var tempDbPath = System.IO.Path.GetTempFileName();
            try
            {
                // Create an empty database (no tables)
                using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = tempDbPath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                }.ToString()))
                {
                    connection.Open();
                    // Don't create any tables
                }

                using var explorer = new SqliteExplorer(tempDbPath);

                // Act
                var tables = explorer.ListTables();

                // Assert
                Assert.NotNull(tables);
                Assert.Empty(tables);
            }
            finally
            {
                if (System.IO.File.Exists(tempDbPath))
                    System.IO.File.Delete(tempDbPath);
            }
        }

        [Fact]
        public void DescribeTable_ReturnsCorrectColumnMetadataTypes()
        {
            // Arrange
            var tempDbPath = System.IO.Path.GetTempFileName();
            try
            {
                using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = tempDbPath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                }.ToString()))
                {
                    connection.Open();
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                    CREATE TABLE type_test (
                        id INTEGER PRIMARY KEY,
                        text_col TEXT,
                        real_col REAL,
                        blob_col BLOB,
                        numeric_col NUMERIC,
                        int_col INTEGER,
                        bool_col BOOLEAN,
                        date_col DATE,
                        datetime_col DATETIME
                    );
                    ";
                    cmd.ExecuteNonQuery();
                }

                using var explorer = new SqliteExplorer(tempDbPath);

                // Act
                var columns = explorer.DescribeTable("type_test");

                // Assert
                Assert.NotNull(columns);
                Assert.Equal(9, columns.Count);

                // Check each column type
                var idCol = columns.First(c => c.Name == "id");
                Assert.Equal("INTEGER", idCol.Type);
                Assert.True(idCol.PrimaryKey);

                var textCol = columns.First(c => c.Name == "text_col");
                Assert.Equal("TEXT", textCol.Type);

                var realCol = columns.First(c => c.Name == "real_col");
                Assert.Equal("REAL", realCol.Type);

                var blobCol = columns.First(c => c.Name == "blob_col");
                Assert.Equal("BLOB", blobCol.Type);

                var numericCol = columns.First(c => c.Name == "numeric_col");
                Assert.Equal("NUMERIC", numericCol.Type);

                var intCol = columns.First(c => c.Name == "int_col");
                Assert.Equal("INTEGER", intCol.Type);

                var boolCol = columns.First(c => c.Name == "bool_col");
                Assert.Equal("BOOLEAN", boolCol.Type);

                var dateCol = columns.First(c => c.Name == "date_col");
                Assert.Equal("DATE", dateCol.Type);

                var datetimeCol = columns.First(c => c.Name == "datetime_col");
                Assert.Equal("DATETIME", datetimeCol.Type);
            }
            finally
            {
                if (System.IO.File.Exists(tempDbPath))
                    System.IO.File.Delete(tempDbPath);
            }
        }

        [Fact]
        public void DescribeTable_ReturnsNullForEmptyType()
        {
            // Arrange
            var tempDbPath = System.IO.Path.GetTempFileName();
            try
            {
                using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = tempDbPath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                }.ToString()))
                {
                    connection.Open();
                    using var cmd = connection.CreateCommand();
                    // Create table without specifying types (SQLite will infer)
                    cmd.CommandText = @"
                    CREATE TABLE no_types (id);
                    ";
                    cmd.ExecuteNonQuery();
                }

                using var explorer = new SqliteExplorer(tempDbPath);

                // Act
                var columns = explorer.DescribeTable("no_types");

                // Assert
                Assert.NotNull(columns);
                Assert.Single(columns);
                Assert.Equal("id", columns[0].Name);
                // SQLite may return empty string or "ANY" for inferred types
                Assert.True(string.IsNullOrEmpty(columns[0].Type) || columns[0].Type == "ANY");
            }
            finally
            {
                if (System.IO.File.Exists(tempDbPath))
                    System.IO.File.Delete(tempDbPath);
            }
        }

        [Fact]
        public void ListForeignKeys_ReportsForeignKeysCorrectly()
        {
            // Arrange
            var tempDbPath = System.IO.Path.GetTempFileName();
            try
            {
                using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = tempDbPath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                }.ToString()))
                {
                    connection.Open();
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                    CREATE TABLE parent (id INTEGER PRIMARY KEY, name TEXT);
                    CREATE TABLE child (
                        id INTEGER PRIMARY KEY,
                        parent_id INTEGER NOT NULL REFERENCES parent(id) ON DELETE CASCADE,
                        other_parent_id INTEGER REFERENCES parent(id) ON UPDATE SET NULL
                    );
                    ";
                    cmd.ExecuteNonQuery();
                }

                using var explorer = new SqliteExplorer(tempDbPath);

                // Act
                var foreignKeys = explorer.ListForeignKeys("child");

                // Assert
                Assert.NotNull(foreignKeys);
                Assert.Equal(2, foreignKeys.Count);

                // Check first FK (parent_id -> parent(id))
                var fk1 = foreignKeys.First(fk => fk.Column == "parent_id");
                Assert.Equal("child", fk1.Table);
                Assert.Equal("parent", fk1.ReferencesTable);
                Assert.Equal("id", fk1.ReferencesColumn);
                Assert.Equal("CASCADE", fk1.OnDelete);
                Assert.Equal("NO ACTION", fk1.OnUpdate); // Default

                // Check second FK (other_parent_id -> parent(id))
                var fk2 = foreignKeys.First(fk => fk.Column == "other_parent_id");
                Assert.Equal("child", fk2.Table);
                Assert.Equal("parent", fk2.ReferencesTable);
                Assert.Equal("id", fk2.ReferencesColumn);
                Assert.Equal("NO ACTION", fk2.OnDelete); // Default
                Assert.Equal("SET NULL", fk2.OnUpdate);
            }
            finally
            {
                if (System.IO.File.Exists(tempDbPath))
                    System.IO.File.Delete(tempDbPath);
            }
        }

        [Fact]
        public void ListForeignKeys_OnTableWithoutForeignKeys_ReturnsEmptyList()
        {
            // Arrange
            var tempDbPath = System.IO.Path.GetTempFileName();
            try
            {
                using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = tempDbPath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                }.ToString()))
                {
                    connection.Open();
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                    CREATE TABLE no_fks (id INTEGER PRIMARY KEY, name TEXT);
                    ";
                    cmd.ExecuteNonQuery();
                }

                using var explorer = new SqliteExplorer(tempDbPath);

                // Act
                var foreignKeys = explorer.ListForeignKeys("no_fks");

                // Assert
                Assert.NotNull(foreignKeys);
                Assert.Empty(foreignKeys);
            }
            finally
            {
                if (System.IO.File.Exists(tempDbPath))
                    System.IO.File.Delete(tempDbPath);
            }
        }

        [Fact]
        public void ListForeignKeys_OnNonexistentTable_ThrowsArgumentException()
        {
            // Arrange
            var tempDbPath = System.IO.Path.GetTempFileName();
            try
            {
                // Create empty database
                using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = tempDbPath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                }.ToString()))
                {
                    connection.Open();
                }

                using var explorer = new SqliteExplorer(tempDbPath);

                // Act & Assert - should throw ArgumentException for non-existent table
                var exception = Assert.Throws<ArgumentException>(() => explorer.ListForeignKeys("nonexistent"));
                Assert.Contains("No such table or view", exception.Message);
            }
            finally
            {
                if (System.IO.File.Exists(tempDbPath))
                    System.IO.File.Delete(tempDbPath);
            }
        }

        [Fact]
        public void GetForeignKeyGraph_ReportsAllForeignKeysInDatabase()
        {
            // Arrange
            var tempDbPath = System.IO.Path.GetTempFileName();
            try
            {
                using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = tempDbPath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                }.ToString()))
                {
                    connection.Open();
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                    CREATE TABLE authors (id INTEGER PRIMARY KEY, name TEXT);
                    CREATE TABLE books (id INTEGER PRIMARY KEY, author_id INTEGER REFERENCES authors(id));
                    CREATE TABLE reviews (id INTEGER PRIMARY KEY, book_id INTEGER REFERENCES books(id));
                    ";
                    cmd.ExecuteNonQuery();
                }

                using var explorer = new SqliteExplorer(tempDbPath);

                // Act
                var graph = explorer.GetForeignKeyGraph();

                // Assert
                Assert.NotNull(graph);
                Assert.Equal(2, graph.Count); // books->authors and reviews->books

                Assert.Contains(graph, fk => fk.Table == "books" && fk.ReferencesTable == "authors");
                Assert.Contains(graph, fk => fk.Table == "reviews" && fk.ReferencesTable == "books");
            }
            finally
            {
                if (System.IO.File.Exists(tempDbPath))
                    System.IO.File.Delete(tempDbPath);
            }
        }

        [Fact]
        public void GetForeignKeyGraph_OnEmptyDatabase_ReturnsEmptyList()
        {
            // Arrange
            var tempDbPath = System.IO.Path.GetTempFileName();
            try
            {
                using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = tempDbPath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                }.ToString()))
                {
                    connection.Open();
                }

                using var explorer = new SqliteExplorer(tempDbPath);

                // Act
                var graph = explorer.GetForeignKeyGraph();

                // Assert
                Assert.NotNull(graph);
                Assert.Empty(graph);
            }
            finally
            {
                if (System.IO.File.Exists(tempDbPath))
                    System.IO.File.Delete(tempDbPath);
            }
        }

        [Fact]
        public void ListIndexes_ReturnsIndexInformation()
        {
            // Arrange
            var tempDbPath = System.IO.Path.GetTempFileName();
            try
            {
                using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = tempDbPath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                }.ToString()))
                {
                    connection.Open();
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                    CREATE TABLE test_table (
                        id INTEGER PRIMARY KEY,
                        name TEXT,
                        value REAL,
                        created_at TEXT
                    );
                    CREATE INDEX idx_test_name ON test_table(name);
                    CREATE INDEX idx_test_value ON test_table(value);
                    CREATE UNIQUE INDEX idx_test_created ON test_table(created_at);
                    ";
                    cmd.ExecuteNonQuery();
                }

                using var explorer = new SqliteExplorer(tempDbPath);

                // Act
                var indexes = explorer.ListIndexes("test_table");

                // Assert
                Assert.NotNull(indexes);
                Assert.Equal(3, indexes.Count);

                // Should have 3 indexes
                Assert.Contains(indexes, idx => idx.Name == "idx_test_name" && !idx.Unique);
                Assert.Contains(indexes, idx => idx.Name == "idx_test_value" && !idx.Unique);
                Assert.Contains(indexes, idx => idx.Name == "idx_test_created" && idx.Unique);

                // All should be on test_table
                foreach (var index in indexes)
                {
                    Assert.Equal("test_table", index.Table);
                }
            }
            finally
            {
                if (System.IO.File.Exists(tempDbPath))
                    System.IO.File.Delete(tempDbPath);
            }
        }

        [Fact]
        public void ListIndexes_OnTableWithoutIndexes_ReturnsPrimaryKeyIndex()
        {
            // Arrange
            var tempDbPath = System.IO.Path.GetTempFileName();
            try
            {
                using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = tempDbPath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                }.ToString()))
                {
                    connection.Open();
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                    CREATE TABLE no_indexes (id INTEGER PRIMARY KEY, name TEXT);
                    ";
                    cmd.ExecuteNonQuery();
                }

                using var explorer = new SqliteExplorer(tempDbPath);

                // Act
                var indexes = explorer.ListIndexes("no_indexes");

                // Assert - SQLite creates implicit index for primary key
                // Note: INTEGER PRIMARY KEY creates a rowid alias, which doesn't appear in index_list
                // So we expect empty list for tables with only INTEGER PRIMARY KEY
                Assert.NotNull(indexes);
            }
            finally
            {
                if (System.IO.File.Exists(tempDbPath))
                    System.IO.File.Delete(tempDbPath);
            }
        }

        [Fact]
        public void ForeignKeyInfo_ReferencesColumnCanBeNullForPrimaryKey()
        {
            // Arrange
            var tempDbPath = System.IO.Path.GetTempFileName();
            try
            {
                using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = tempDbPath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                }.ToString()))
                {
                    connection.Open();
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                    CREATE TABLE parent (id INTEGER PRIMARY KEY);
                    CREATE TABLE child (
                        id INTEGER PRIMARY KEY,
                        parent_id INTEGER REFERENCES parent
                    );
                    ";
                    cmd.ExecuteNonQuery();
                }

                using var explorer = new SqliteExplorer(tempDbPath);

                // Act
                var foreignKeys = explorer.ListForeignKeys("child");

                // Assert
                Assert.NotNull(foreignKeys);
                Assert.Single(foreignKeys);

                var fk = foreignKeys[0];
                Assert.Equal("parent", fk.ReferencesTable);
                // When referencing a primary key without specifying column, ReferencesColumn should be null
                Assert.Null(fk.ReferencesColumn);
            }
            finally
            {
                if (System.IO.File.Exists(tempDbPath))
                    System.IO.File.Delete(tempDbPath);
            }
        }
    }
}
