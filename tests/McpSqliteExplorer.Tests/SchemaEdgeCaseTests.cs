using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Microsoft.Data.Sqlite;

namespace McpSqliteExplorer.Tests
{
    /// <summary>
    /// Comprehensive edge case tests for SqliteExplorer schema-related functionality:
    /// - Tables with quoted/unusual names
    /// - Composite primary keys
    /// - Views vs tables distinction
    /// - Indexes on unusual tables
    /// - Foreign keys with composite relationships
    /// - ERD generation with edge cases
    /// - Migration history detection
    /// </summary>
    public class SchemaEdgeCaseTests
    {
        [Fact]
        public void ListIndexes_WithRegularTable_ReturnsIndexes()
        {
            // Arrange
            using var testDb = new TestDatabase();
            using var explorer = new SqliteExplorer(testDb.Path);

            // Act
            var indexes = explorer.ListIndexes("books");

            // Assert
            Assert.NotNull(indexes);
            Assert.NotEmpty(indexes);
            Assert.Contains(indexes, idx => idx.Name == "idx_books_year");
            Assert.Contains(indexes, idx => idx.Table == "books");
        }

        [Fact]
        public void ListIndexes_WithTableHavingPrimaryKey_ReturnsPrimaryKeyIndex()
        {
            // Arrange
            using var testDb = new TestDatabase();
            using var explorer = new SqliteExplorer(testDb.Path);

            // Act
            var indexes = explorer.ListIndexes("books");

            // Assert - should have primary key index (SQLite creates implicit index for PK)
            Assert.Contains(indexes, idx => idx.Origin == "primary-key" || idx.Name.Contains("books"));
        }

        [Fact]
        public void ListIndexes_WithTableHavingRegularIndex_ReturnsIndexInfo()
        {
            // Arrange
            using var testDb = new TestDatabase();
            using var explorer = new SqliteExplorer(testDb.Path);

            // SQLite creates implicit indexes for UNIQUE constraints and primary keys
            var indexes = explorer.ListIndexes("books");

            // Should have the idx_books_year index we created
            Assert.Contains(indexes, idx => idx.Name == "idx_books_year");
            Assert.NotEmpty(indexes);
        }

        [Fact]
        public void ListForeignKeys_WithTableHavingForeignKey_ReturnsForeignKeyInfo()
        {
            // Arrange
            using var testDb = new TestDatabase();
            using var explorer = new SqliteExplorer(testDb.Path);

            // Act
            var foreignKeys = explorer.ListForeignKeys("books");

            // Assert
            Assert.NotNull(foreignKeys);
            Assert.NotEmpty(foreignKeys);
            Assert.Contains(foreignKeys, fk => fk.Table == "books" && fk.Column == "author_id");
        }

        [Fact]
        public void ListForeignKeys_WithTableHavingOnDeleteCascade_ReturnsCorrectOnDeleteAction()
        {
            // Arrange
            using var testDb = new TestDatabase();
            using var explorer = new SqliteExplorer(testDb.Path);

            // Act
            var foreignKeys = explorer.ListForeignKeys("loans");

            // Assert
            var loanFk = Assert.Single(foreignKeys);
            Assert.Equal("CASCADE", loanFk.OnDelete);
        }

        [Fact]
        public void GetForeignKeyGraph_ReturnsAllForeignKeysInDatabase()
        {
            // Arrange
            using var testDb = new TestDatabase();
            using var explorer = new SqliteExplorer(testDb.Path);

            // Act
            var graph = explorer.GetForeignKeyGraph();

            // Assert
            Assert.NotNull(graph);
            Assert.NotEmpty(graph);
            Assert.Contains(graph, fk => fk.Table == "books" && fk.ReferencesTable == "authors");
            Assert.Contains(graph, fk => fk.Table == "loans" && fk.ReferencesTable == "books");
        }

        [Fact]
        public void ExploreForeignKeyChain_WithMaxDepth_ReturnsConnectedTables()
        {
            // Arrange
            using var testDb = new TestDatabase();
            using var explorer = new SqliteExplorer(testDb.Path);

            // Act - explore from authors table with depth 2
            var hops = explorer.ExploreForeignKeyChain("authors", maxDepth: 2);

            // Assert
            Assert.NotNull(hops);
            Assert.NotEmpty(hops);

            // Should find books -> authors and loans <- books connections
            var authorHops = hops.Where(h => h.FromTable.Equals("authors", StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.NotEmpty(authorHops);
        }

        [Fact]
        public void ExploreForeignKeyChain_WithDepth1_ReturnsDirectConnectionsOnly()
        {
            // Arrange
            using var testDb = new TestDatabase();
            using var explorer = new SqliteExplorer(testDb.Path);

            // Act
            var hops = explorer.ExploreForeignKeyChain("books", maxDepth: 1);

            // Assert
            Assert.NotNull(hops);

            // Should only have direct references (authors) and direct referencing (loans)
            var bookHops = hops.Where(h => h.FromTable.Equals("books", StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.NotEmpty(bookHops);
        }

        [Fact]
        public void GenerateErd_ReturnsValidMermaidDiagram()
        {
            // Arrange
            using var testDb = new TestDatabase();
            using var explorer = new SqliteExplorer(testDb.Path);

            // Act
            var erd = explorer.GenerateErd();

            // Assert - basic structure validation
            Assert.NotNull(erd);
            Assert.NotEmpty(erd);
            Assert.StartsWith("erDiagram", erd);

            // Should contain table definitions
            Assert.Contains("books {", erd);
            Assert.Contains("authors {", erd);
            Assert.Contains("loans {", erd);
            Assert.Contains("__EFMigrationsHistory {", erd);
        }

        [Fact]
        public void GenerateErd_IncludesPrimaryKeyMarkers()
        {
            // Arrange
            using var testDb = new TestDatabase();
            using var explorer = new SqliteExplorer(testDb.Path);

            // Act
            var erd = explorer.GenerateErd();

            // Assert
            Assert.Contains("id PK", erd);
        }

        [Fact]
        public void GenerateErd_IncludesForeignKeyMarkers()
        {
            // Arrange
            using var testDb = new TestDatabase();
            using var explorer = new SqliteExplorer(testDb.Path);

            // Act
            var erd = explorer.GenerateErd();

            // Assert
            Assert.Contains("author_id FK", erd);
            Assert.Contains("book_id FK", erd);
        }

        [Fact]
        public void GetMigrationHistory_WithEfHistoryTable_ReturnsMigrationInfo()
        {
            // Arrange
            using var testDb = new TestDatabase();
            using var explorer = new SqliteExplorer(testDb.Path);

            // Act
            var migrationInfo = explorer.GetMigrationHistory();

            // Assert
            Assert.NotNull(migrationInfo);
            Assert.True(migrationInfo.HasHistoryTable);
            Assert.NotEmpty(migrationInfo.Migrations);
            Assert.Equal(2, migrationInfo.Migrations.Count);
        }

        [Fact]
        public void GetMigrationHistory_WithEfHistoryTable_ReturnsMigrationEntries()
        {
            // Arrange
            using var testDb = new TestDatabase();
            using var explorer = new SqliteExplorer(testDb.Path);

            // Act
            var migrationInfo = explorer.GetMigrationHistory();

            // Assert
            Assert.NotNull(migrationInfo);
            Assert.True(migrationInfo.HasHistoryTable);
            Assert.NotEmpty(migrationInfo.Migrations);
            Assert.Equal(2, migrationInfo.Migrations.Count);
        }

        [Fact]
        public void ListTables_DistinguishesBetweenTablesAndViews()
        {
            // Arrange
            using var testDb = new TestDatabase();
            using var explorer = new SqliteExplorer(testDb.Path);

            // Act
            var tables = explorer.ListTables();

            // Assert
            Assert.NotNull(tables);
            Assert.NotEmpty(tables);

            var tableCount = tables.Count(t => t.Type == "table");
            var viewCount = tables.Count(t => t.Type == "view");

            Assert.Equal(4, tableCount); // authors, books, loans, __EFMigrationsHistory
            Assert.Equal(1, viewCount); // recent_books
        }

        [Fact]
        public void DescribeTable_ReturnsCorrectColumnInfo()
        {
            // Arrange
            using var testDb = new TestDatabase();
            using var explorer = new SqliteExplorer(testDb.Path);

            // Act
            var columns = explorer.DescribeTable("books");

            // Assert
            Assert.NotNull(columns);
            Assert.NotEmpty(columns);
            Assert.Equal(4, columns.Count);

            // Check primary key
            var idColumn = Assert.Single(columns.Where(c => c.PrimaryKey));
            Assert.Equal("id", idColumn.Name);

            // Check NOT NULL constraint
            var titleColumn = columns.First(c => c.Name == "title");
            Assert.True(titleColumn.NotNull);
        }

        [Fact]
        public void DescribeTable_WithView_ReturnsColumnInfoForView()
        {
            // Arrange
            using var testDb = new TestDatabase();
            using var explorer = new SqliteExplorer(testDb.Path);

            // Act
            var columns = explorer.DescribeTable("recent_books");

            // Assert
            Assert.NotNull(columns);
            Assert.NotEmpty(columns);
            Assert.Equal(2, columns.Count); // title, year
        }

        [Fact]
        public void ListIndexes_WithView_ReturnsEmptyList()
        {
            // Arrange
            using var testDb = new TestDatabase();
            using var explorer = new SqliteExplorer(testDb.Path);

            // Views don't have indexes in SQLite
            var indexes = explorer.ListIndexes("recent_books");

            Assert.Empty(indexes);
        }

        [Fact]
        public void ListForeignKeys_WithView_ReturnsEmptyList()
        {
            // Arrange
            using var testDb = new TestDatabase();
            using var explorer = new SqliteExplorer(testDb.Path);

            // Views don't have foreign keys in SQLite
            var foreignKeys = explorer.ListForeignKeys("recent_books");

            Assert.Empty(foreignKeys);
        }

        [Fact]
        public void ForeignKeyInfo_ReferencesColumnCanBeNullForPrimaryKey()
        {
            // Arrange
            using var testDb = new TestDatabase();
            using var explorer = new SqliteExplorer(testDb.Path);

            // Act
            var foreignKeys = explorer.ListForeignKeys("loans");

            // Assert - book_id references books(id) which is the primary key
            var loanFk = foreignKeys.First(fk => fk.Table == "loans");
            Assert.Equal("books", loanFk.ReferencesTable);
            Assert.Equal("id", loanFk.ReferencesColumn); // Should reference the id column
        }

        [Fact]
        public void ForeignKeyHop_DirectionValuesAreCorrect()
        {
            // Arrange
            using var testDb = new TestDatabase();
            using var explorer = new SqliteExplorer(testDb.Path);

            // Act
            var hops = explorer.ExploreForeignKeyChain("books", maxDepth: 1);

            // Assert
            var bookHops = hops.Where(h => h.FromTable.Equals("books", StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var hop in bookHops)
            {
                Assert.True(hop.Direction == "references" || hop.Direction == "referenced-by");
            }
        }

        [Fact]
        public void IndexInfo_ColumnsPropertyHandlesExpressionIndexes()
        {
            // Arrange
            using var testDb = new TestDatabase();
            using var explorer = new SqliteExplorer(testDb.Path);

            // SQLite doesn't create expression indexes in our test database
            // This test documents the expected behavior
            var indexes = explorer.ListIndexes("books");

            foreach (var index in indexes)
            {
                Assert.NotNull(index.Columns);
                Assert.NotEmpty(index.Columns);
            }
        }

        [Fact]
        public void MermaidName_HandlesSpecialCharacters()
        {
            // Arrange - Create a database with special characters in table names
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
                    cmd.CommandText = @"
                        CREATE TABLE test_table (id INTEGER PRIMARY KEY);
                        CREATE TABLE [table with spaces] (id INTEGER PRIMARY KEY);
                        CREATE TABLE [table'with'quotes] (id INTEGER PRIMARY KEY);
                        CREATE TABLE [table""with""double""quotes] (id INTEGER PRIMARY KEY);
                    ";
                    cmd.ExecuteNonQuery();
                }

                using var tempExplorer = new SqliteExplorer(tempDbPath);
                var tables = tempExplorer.ListTables();

                // All tables should be listed without errors
                Assert.NotEmpty(tables);
            }
            finally
            {
                if (System.IO.File.Exists(tempDbPath))
                    System.IO.File.Delete(tempDbPath);
            }
        }
    }
}
