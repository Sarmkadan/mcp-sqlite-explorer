# SqliteTools

`SqliteTools` is a utility class providing pre-defined SQL query templates for common SQLite database operations. These static string members facilitate tasks such as listing tables, describing table structures, analyzing query execution plans, and retrieving sample data. The class is designed to streamline interactions with SQLite databases by offering standardized SQL snippets that can be directly executed or modified as needed.

## API

### `ListTables`

**Purpose**: Retrieves a list of all tables in the connected SQLite database.

**Parameters**: None.

**Return Value**: A SQL query string that selects table names from the `sqlite_master` system table.

**Exceptions**: Does not throw exceptions. However, executing the returned query may throw if the database connection is invalid or inaccessible.

---

### `DescribeTable`

**Purpose**: Generates a SQL query to retrieve the schema definition of a specified table.

**Parameters**: None. The target table name must be interpolated into the returned query string.

**Return Value**: A SQL query template that uses `PRAGMA table_info(tableName)` to describe the columns of a table.

**Exceptions**: Does not throw exceptions. Execution may fail if the table does not exist or the database is locked.

---

### `ListIndexes`

**Purpose**: Lists all indexes defined in the SQLite database.

**Parameters**: None.

**Return Value**: A SQL query string that selects index names and associated table names from `sqlite_master`.

**Exceptions**: Does not throw exceptions. Execution may fail if the database is unavailable.

---

### `SampleRows`

**Purpose**: Retrieves a limited number of sample rows from a specified table.

**Parameters**: None. The target table name and row limit must be interpolated into the returned query string.

**Return Value**: A SQL query template using `SELECT * FROM tableName LIMIT limit`.

**Exceptions**: Does not throw exceptions. Execution may fail if the table does not exist or the limit is invalid.

---

### `RunSelect`

**Purpose**: Executes a custom SELECT query on the database.

**Parameters**: None. The query string must be provided externally.

**Return Value**: A placeholder string indicating where a user-defined SELECT query should be inserted.

**Exceptions**: Does not throw exceptions. Execution depends on the validity of the provided query.

---

### `ExplainQuery`

**Purpose**: Analyzes the execution plan of a provided SQL query.

**Parameters**: None. The target query must be interpolated into the returned SQL string.

**Return Value**: A SQL query template using `EXPLAIN QUERY PLAN query` to describe the query's execution strategy.

**Exceptions**: Does not throw exceptions. Execution may fail if the query is malformed.

---

### `TableStats`

**Purpose**: Retrieves statistical information about a specified table's storage and row count.

**Parameters**: None. The target table name must be interpolated into the returned query string.

**Return Value**: A SQL query template using `PRAGMA tableName.statistics` to gather table statistics.

**Exceptions**: Does not throw exceptions. Execution may fail if the table does not exist or statistics are unavailable.

---

## Usage

### Example 1: Listing Tables and Describing a Schema

```csharp
using (var connection = new SQLiteConnection("Data Source=mydatabase.db"))
{
    connection.Open();
    
    // List all tables
    var listTablesCmd = connection.CreateCommand();
    listTablesCmd.CommandText = SqliteTools.ListTables;
    using (var reader = listTablesCmd.ExecuteReader())
    {
        while (reader.Read())
        {
            Console.WriteLine($"Table: {reader["name"]}");
        }
    }

    // Describe a specific table
    var describeCmd = connection.CreateCommand();
    describeCmd.CommandText = string.Format(SqliteTools.DescribeTable, "users");
    using (var reader = describeCmd.ExecuteReader())
    {
        while (reader.Read())
        {
            Console.WriteLine($"Column: {reader["name"]}, Type: {reader["type"]}");
        }
    }
}
```

### Example 2: Analyzing Query Performance

```csharp
using (var connection = new SQLiteConnection("Data Source=mydatabase.db"))
{
    connection.Open();
    
    var explainCmd = connection.CreateCommand();
    explainCmd.CommandText = string.Format(
        SqliteTools.ExplainQuery, 
        "SELECT * FROM users WHERE age > 30"
    );
    
    using (var reader = explainCmd.ExecuteReader())
    {
        while (reader.Read())
        {
            Console.WriteLine($"Detail: {reader["detail"]}");
        }
    }
}
```

---

## Notes

- All members are static and return immutable strings, making them inherently thread-safe for concurrent read access.
- Queries requiring table names or dynamic values (e.g., `DescribeTable`, `SampleRows`) expect string interpolation before execution. Invalid inputs may result in runtime errors during query execution.
- `TableStats` relies on SQLite's internal statistics, which may not be populated unless the database has been analyzed using `ANALYZE`. Results may be incomplete or unavailable in such cases.
- `RunSelect` is a placeholder and requires external query input; it does not validate or sanitize user-provided SQL.
- These templates assume standard SQLite system tables (`sqlite_master`) and may not function correctly with custom-compiled SQLite variants or extensions that alter system schema behavior.
