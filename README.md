
## SqliteToolsFormattingTests

The SqliteToolsFormattingTests class contains tests for the SqliteToolsFormatting class. It checks the formatting of Sqlite database tables.

Example usage:
```csharp
public SqliteToolsFormattingTests
public void Dispose
public void ListTables_EmptyDatabase_ReturnsEmptyList
public void DescribeTable_TableWithAllColumnTypes_ReturnsCorrectDescription
public void SampleRows_TableWithRows_RowCapEnforced
```

## SqliteAnalysisToolsTests

The SqliteAnalysisToolsTests class verifies the read-only analysis surface provided by the SqliteAnalysisTools class. It ensures that various analysis tools, such as ERD generation, query plan explanation, and table statistics, return correct JSON results when executed against a populated SQLite database.

Example usage demonstrating SqliteAnalysisTools functionality:
```csharp
// Example using SqliteAnalysisTools as validated by SqliteAnalysisToolsTests
using McpSqliteExplorer;

var explorer = new SqliteExplorer("path/to/database.db");

// Explain a query plan
string planJson = SqliteAnalysisTools.ExplainQueryPlan(explorer, "SELECT * FROM users WHERE id = 1");

// Generate an ERD in Mermaid format
string erdJson = SqliteAnalysisTools.GenerateErd(explorer);

// Get table statistics
string statsJson = SqliteAnalysisTools.TableStatsOverview(explorer);
```
