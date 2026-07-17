# SqliteExplorerTestsExtensions

`SqliteExplorerTestsExtensions` is a static helper class that provides convenient methods for creating and interacting with test databases during unit testing of the `SqliteExplorer` API. It encapsulates common patterns such as creating an in‑memory database, retrieving values from query results, and asserting table state, thereby reducing boilerplate in test suites.

## API

### `public static (SqliteExplorer Explorer, string DbPath) CreateTestDatabase`

Creates a temporary SQLite database file, populates it with a predefined schema and data, and returns a tuple containing a `SqliteExplorer` instance configured to use that database and the file path.  
**Parameters**: None.  
**Return value**: A tuple where `Explorer` is a fully initialized `SqliteExplorer` and `DbPath` is the absolute path to the temporary database file.  
**Throws**: `IOException` if the temporary file cannot be created; `SqliteException` if the database cannot be initialized.

### `public static SqliteExplorer CreateExplorer`

Creates a new `SqliteExplorer` instance that connects to an in‑memory SQLite database. The database is automatically populated with a default schema suitable for unit tests.  
**Parameters**: None.  
**Return value**: A `SqliteExplorer` instance.  
**Throws**: `SqliteException` if the connection cannot be established.

### `public static T GetValue<T>(this SqliteExplorerTests _, QueryResult result, string columnName)`

Retrieves the value of the specified column from the first row of a `QueryResult`.  
**Parameters**:  
- `result`: The `QueryResult` containing the data.  
- `columnName`: The name of the column to extract.  
**Return value**: The value cast to type `T`.  
**Throws**: `ArgumentException` if the column does not exist; `InvalidCastException` if the value cannot be cast to `T`; `IndexOutOfRangeException` if the result has no rows.

### `public static T GetValue<T>(this SqliteExplorerTests _, QueryResult result, int rowIndex, string columnName)`

Retrieves the value of the specified column from the row at `rowIndex` in a `QueryResult`.  
**Parameters**:  
- `result`: The `QueryResult` containing the data.  
- `rowIndex`: Zero‑based index of the row to read.  
- `columnName`: The name of the column to extract.  
**Return value**: The value cast to type `T`.  
**Throws**: `ArgumentException` if the column does not exist; `IndexOutOfRangeException` if `rowIndex` is out of bounds; `InvalidCastException` if the value cannot be cast to `T`.

### `public static void AssertTableRowCount(this SqliteExplorerTests _, SqliteExplorer explorer, string tableName, int expectedCount)`

Asserts that the specified table contains exactly `expectedCount` rows.  
**Parameters**:  
- `explorer`: The `SqliteExplorer` instance.  
- `tableName`: Name of the table to inspect.  
- `expectedCount`: The expected number of rows.  
**Return value**: None.  
**Throws**: `AssertFailedException` if the actual row count differs from `expectedCount`; `ArgumentException` if the table does not exist.

### `public static void AssertFirstRowValue<T>(this SqliteExplorerTests _, SqliteExplorer explorer, string tableName, string columnName, T expectedValue)`

Asserts that the first row of the specified table contains `expectedValue` in the given column.  
**Parameters**:  
- `explorer`: The `SqliteExplorer` instance.  
- `tableName`: Name of the table to inspect.  
- `columnName`: Name of the column to check.  
- `expectedValue`: The value expected in the first row.  
**Return value**: None.  
**Throws**: `AssertFailedException` if the value does not match; `ArgumentException` if the table or column does not exist; `IndexOutOfRangeException` if the table is empty.

## Usage

