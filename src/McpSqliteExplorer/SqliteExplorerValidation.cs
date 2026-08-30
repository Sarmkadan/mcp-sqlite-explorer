using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace McpSqliteExplorer;

/// <summary>
/// Custom exception thrown when validation of SQLite identifiers (table names, column names, etc.) fails.
/// This provides a consistent error type across SqliteTools, SqliteAnalysisTools, and SqliteExplorer.
/// </summary>
public sealed class SqliteExplorerValidationException : ArgumentException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteExplorerValidationException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public SqliteExplorerValidationException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteExplorerValidationException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="paramName">The name of the parameter that caused the current exception.</param>
    public SqliteExplorerValidationException(string message, string? paramName) : base(message, paramName) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteExplorerValidationException"/> class with serialized data.
    /// </summary>
    /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
    /// <param name="context">The <see cref="System.Runtime.Serialization.StreamingContext"/> that contains contextual information about the source or destination.</param>
    private SqliteExplorerValidationException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
}

/// <summary>
/// Provides validation helpers for <see cref="SqliteExplorer"/> instances and its related record types.
/// </summary>
public static class SqliteExplorerValidation
{
    // Maximum number of characters accepted in a SQLite identifier.
    private const int MaxIdentifierLength = 255;

    // Count representing an empty validation-problem or result-column collection.
    private const int EmptyCount = 0;

    // Lowest row-cap value accepted for a query result.
    private const int MinimumAppliedRowCap = 1;

    // Maximum number of statement-separating semicolons allowed in a SQL query.
    private const int MaxStatementSeparators = 0;

    // SQL keywords reused by metadata and validation checks.
    private const string AlterKeyword = "ALTER";
    private const string AttachKeyword = "ATTACH";
    private const string CreateKeyword = "CREATE";
    private const string DeleteKeyword = "DELETE";
    private const string DetachKeyword = "DETACH";
    private const string DropKeyword = "DROP";
    private const string InsertKeyword = "INSERT";
    private const string PragmaKeyword = "PRAGMA";
    private const string ReplaceKeyword = "REPLACE";
    private const string SelectKeyword = "SELECT";
    private const string TableKeyword = "TABLE";
    private const string TruncateKeyword = "TRUNCATE";
    private const string UnionKeyword = "UNION";
    private const string UpdateKeyword = "UPDATE";
    private const string VacuumKeyword = "VACUUM";
    private const string ViewKeyword = "VIEW";

    // SQL keywords that identify write operations in otherwise read-only SQL.
    private static readonly string[] WriteKeywords =
    {
        InsertKeyword, UpdateKeyword, DeleteKeyword, ReplaceKeyword, UnionKeyword
    };

    // SQL keywords that identify schema-changing or otherwise dangerous statements.
    private static readonly string[] DangerousKeywords =
    {
        DropKeyword, AlterKeyword, TruncateKeyword, CreateKeyword, AttachKeyword, DetachKeyword,
        VacuumKeyword, PragmaKeyword
    };

    // Non-keyword substrings treated as SQL-injection markers in identifiers.
    private const string ExecuteMarker = "EXEC";
    private const string LineCommentMarker = "--";
    private const string StatementSeparatorMarker = ";";
    private const string BlockCommentStartMarker = "/*";
    private const string BlockCommentEndMarker = "*/";
    private const string OrExpressionMarker = " OR ";
    private const string AndExpressionMarker = " AND ";
    private const string ExtendedProcedureMarker = "XP_";
    private const string WaitForMarker = "WAITFOR";
    private const string ShutdownMarker = "SHUTDOWN";

    /// <summary>
    /// Validates the supplied <see cref="SqliteExplorer"/> instance and returns a list of human-readable problems.
    /// </summary>
    /// <remarks>
    /// <see cref="SqliteExplorer"/> instances themselves have no mutable state to validate.
    /// Validation is performed on the records returned by explorer operations.
    /// </remarks>
    /// <param name="value">The explorer instance to validate.</param>
    /// <returns>
    /// An <see cref="IReadOnlyList{T}"/> of problem descriptions.
    /// The list is empty when the instance is considered valid.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    public static IReadOnlyList<string> Validate(this SqliteExplorer value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Array.Empty<string>();
    }

    /// <summary>
    /// Determines whether the supplied <see cref="SqliteExplorer"/> instance is valid.
    /// </summary>
    /// <param name="value">The explorer instance to check.</param>
    /// <returns><c>true</c> if no validation problems are reported; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    public static bool IsValid(this SqliteExplorer value) => value is not null && !value.Validate().Any();

    /// <summary>
    /// Ensures that the supplied <see cref="SqliteExplorer"/> instance is valid.
    /// </summary>
    /// <param name="value">The explorer instance to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when one or more validation problems are detected. The exception message contains a
    /// semicolon-separated list of the problems.
    /// </exception>
    public static void EnsureValid(this SqliteExplorer value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _ = value.Validate(); // Validate call already includes null check
    }

    /// <summary>
    /// Validates the supplied <see cref="TableInfo"/> record and returns a list of human-readable problems.
    /// </summary>
    /// <param name="value">The table info to validate.</param>
    /// <returns>
    /// An <see cref="IReadOnlyList{T}"/> of problem descriptions.
    /// The list is empty when the instance is considered valid.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    public static IReadOnlyList<string> Validate(this TableInfo value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(value.Name))
            problems.Add("TableInfo.Name must not be null or whitespace");

        if (string.IsNullOrWhiteSpace(value.Type))
            problems.Add("TableInfo.Type must not be null or whitespace");
        else if (value.Type.ToUpperInvariant() is not (TableKeyword or ViewKeyword))
            problems.Add("TableInfo.Type must be either 'table' or 'view'");

        return problems;
    }

    /// <summary>
    /// Determines whether the supplied <see cref="TableInfo"/> record is valid.
    /// </summary>
    /// <param name="value">The table info to check.</param>
    /// <returns><c>true</c> if no validation problems are reported; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    public static bool IsValid(this TableInfo value) => value.Validate().Count == EmptyCount;

    /// <summary>
    /// Ensures that the supplied <see cref="TableInfo"/> record is valid.
    /// </summary>
    /// <param name="value">The table info to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when one or more validation problems are detected. The exception message contains a
    /// semicolon-separated list of the problems.
    /// </exception>
    public static void EnsureValid(this TableInfo value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = value.Validate();
        if (problems.Count > EmptyCount)
        {
            var message = $"TableInfo instance is invalid: {string.Join("; ", problems)}";
            throw new ArgumentException(message, nameof(value));
        }
    }

    /// <summary>
    /// Validates the supplied <see cref="ColumnInfo"/> record and returns a list of human-readable problems.
    /// </summary>
    /// <param name="value">The column info to validate.</param>
    /// <returns>
    /// An <see cref="IReadOnlyList{T}"/> of problem descriptions.
    /// The list is empty when the instance is considered valid.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    public static IReadOnlyList<string> Validate(this ColumnInfo value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(value.Name))
            problems.Add("ColumnInfo.Name must not be null or whitespace");

        if (string.IsNullOrWhiteSpace(value.Type))
            problems.Add("ColumnInfo.Type must not be null or whitespace");

        // DefaultValue can be null, but if not null should be non-empty
        if (value.DefaultValue is not null && string.IsNullOrWhiteSpace(value.DefaultValue))
            problems.Add("ColumnInfo.DefaultValue must not be empty if specified");

        return problems;
    }

    /// <summary>
    /// Determines whether the supplied <see cref="ColumnInfo"/> record is valid.
    /// </summary>
    /// <param name="value">The column info to check.</param>
    /// <returns><c>true</c> if no validation problems are reported; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    public static bool IsValid(this ColumnInfo value) => value.Validate().Count == EmptyCount;

    /// <summary>
    /// Ensures that the supplied <see cref="ColumnInfo"/> record is valid.
    /// </summary>
    /// <param name="value">The column info to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when one or more validation problems are detected. The exception message contains a
    /// semicolon-separated list of the problems.
    /// </exception>
    public static void EnsureValid(this ColumnInfo value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = value.Validate();
        if (problems.Count > EmptyCount)
        {
            var message = $"ColumnInfo instance is invalid: {string.Join("; ", problems)}";
            throw new ArgumentException(message, nameof(value));
        }
    }

    /// <summary>
    /// Validates the supplied <see cref="QueryResult"/> record and returns a list of human-readable problems.
    /// </summary>
    /// <param name="value">The query result to validate.</param>
    /// <returns>
    /// An <see cref="IReadOnlyList{T}"/> of problem descriptions.
    /// The list is empty when the instance is considered valid.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    public static IReadOnlyList<string> Validate(this QueryResult value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = new List<string>();

        if (value.Columns is null)
            problems.Add("QueryResult.Columns must not be null");
        else if (value.Columns.Count == EmptyCount)
            problems.Add("QueryResult.Columns must contain at least one column");
        else
        {
            for (var i = 0; i < value.Columns.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(value.Columns[i]))
                    problems.Add($"QueryResult.Columns[{i}] must not be null or whitespace");
            }
        }

        if (value.Rows is null)
            problems.Add("QueryResult.Rows must not be null");
        else if (value.Columns is not null && value.Columns.Count > EmptyCount)
        {
            // Validate each row has the correct number of columns
            foreach (var row in value.Rows)
            {
                if (row is null)
                {
                    problems.Add("QueryResult.Rows must not contain null rows");
                    continue;
                }

                var rowCount = row.Count;
                if (rowCount != value.Columns.Count)
                    problems.Add($"QueryResult.Rows contains a row with {rowCount} columns, expected {value.Columns.Count}");
            }
        }

        if (value.AppliedRowCap < MinimumAppliedRowCap)
            problems.Add("QueryResult.AppliedRowCap must be a positive integer");
        else if (value.AppliedRowCap > SqliteExplorer.MaxRowCap)
            problems.Add($"QueryResult.AppliedRowCap must not exceed {SqliteExplorer.MaxRowCap}");

        return problems;
    }

    /// <summary>
    /// Determines whether the supplied <see cref="QueryResult"/> record is valid.
    /// </summary>
    /// <param name="value">The query result to check.</param>
    /// <returns><c>true</c> if no validation problems are reported; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    public static bool IsValid(this QueryResult value) => value.Validate().Count == EmptyCount;

    /// <summary>
    /// Ensures that the supplied <see cref="QueryResult"/> record is valid.
    /// </summary>
    /// <param name="value">The query result to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when one or more validation problems are detected. The exception message contains a
    /// semicolon-separated list of the problems.
    /// </exception>
    public static void EnsureValid(this QueryResult value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = value.Validate();
        if (problems.Count > EmptyCount)
        {
            var message = $"QueryResult instance is invalid: {string.Join("; ", problems)}";
            throw new ArgumentException(message, nameof(value));
        }
    }

    /// <summary>
    /// Validates that a table or column identifier is safe to use in SQL statements.
    /// Checks for SQL injection attempts, malformed identifiers, and other security issues.
    /// </summary>
    /// <param name="identifier">The identifier to validate (e.g., table name, column name).</param>
    /// <param name="allowReservedKeywords">Whether to allow SQLite reserved keywords (they can be used if quoted).</param>
    /// <returns>An <see cref="IReadOnlyList{T}"/> of problem descriptions. Empty list if valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="identifier"/> is <c>null</c>.</exception>
    public static IReadOnlyList<string> ValidateIdentifier(string identifier, bool allowReservedKeywords = false)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return new List<string> { "Identifier must not be empty or whitespace" };
        }

        var problems = new List<string>();
        var trimmedIdentifier = identifier.Trim();

        // Check for null or whitespace after trimming
        if (string.IsNullOrWhiteSpace(trimmedIdentifier))
        {
            problems.Add("Identifier must not be empty or whitespace after trimming");
            return problems;
        }

        // Check maximum length (reasonable limit for identifiers)
        if (trimmedIdentifier.Length > MaxIdentifierLength)
        {
            problems.Add($"Identifier exceeds maximum length of {MaxIdentifierLength} characters");
        }

        // Check for SQL injection patterns
        if (IsSqlInjectionAttempt(trimmedIdentifier))
        {
            problems.Add("Identifier contains SQL injection attempt");
        }

        // Check for malformed identifiers with embedded quotes or brackets
        if (IsMalformedIdentifier(trimmedIdentifier))
        {
            problems.Add("Identifier contains invalid characters or improper escaping");
        }

        // Check for leading/trailing whitespace that wasn't trimmed properly
        if (identifier != trimmedIdentifier)
        {
            problems.Add("Identifier contains leading or trailing whitespace");
        }

        // Check for reserved keywords if not allowed
        if (!allowReservedKeywords && IsReservedKeyword(trimmedIdentifier))
        {
            problems.Add("Identifier is a SQLite reserved keyword (use quoted identifier if intentional)");
        }

        // Check for non-ASCII characters (can be used but may cause issues)
        if (!IsAscii(trimmedIdentifier))
        {
            problems.Add("Identifier contains non-ASCII characters (may cause compatibility issues)");
        }

        return problems;
    }

    /// <summary>
    /// Determines whether the supplied identifier is valid.
    /// </summary>
    /// <param name="identifier">The identifier to check.</param>
    /// <param name="allowReservedKeywords">Whether to allow SQLite reserved keywords.</param>
    /// <returns><c>true</c> if no validation problems are reported; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="identifier"/> is <c>null</c>.</exception>
    public static bool IsValidIdentifier(string identifier, bool allowReservedKeywords = false) =>
        identifier is not null && !ValidateIdentifier(identifier, allowReservedKeywords).Any();

    /// <summary>
    /// Ensures that the supplied identifier is valid.
    /// </summary>
    /// <param name="identifier">The identifier to check.</param>
    /// <param name="allowReservedKeywords">Whether to allow SQLite reserved keywords.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="identifier"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when the identifier is invalid.</exception>
    public static void EnsureValidIdentifier(string identifier, bool allowReservedKeywords = false)
    {
        if (identifier is null)
        {
            throw new ArgumentNullException(nameof(identifier));
        }

        var problems = ValidateIdentifier(identifier, allowReservedKeywords);
        if (problems.Count > EmptyCount)
        {
            var message = $"Identifier is invalid: {string.Join("; ", problems)}";
            throw new ArgumentException(message, nameof(identifier));
        }
    }

    /// <summary>
    /// Validates that a SQL query string is safe to execute.
    /// Checks for write statements, multiple statements, and other dangerous patterns.
    /// </summary>
    /// <param name="sql">The SQL query to validate.</param>
    /// <returns>An <see cref="IReadOnlyList{T}"/> of problem descriptions. Empty list if valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sql"/> is <c>null</c>.</exception>
    public static IReadOnlyList<string> ValidateSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return new List<string> { "SQL statement must not be empty or whitespace" };
        }

        var problems = new List<string>();
        var trimmedSql = sql.Trim();

        // Check for empty SQL after trimming
        if (string.IsNullOrWhiteSpace(trimmedSql))
        {
            problems.Add("SQL statement must not be empty or whitespace after trimming");
            return problems;
        }

        // Check for multiple statements (SQL injection via batching)
        if (ContainsMultipleStatements(trimmedSql))
        {
            problems.Add("SQL statement contains multiple statements separated by semicolon");
        }

        // Check for write operations
        if (ContainsWriteKeywords(trimmedSql))
        {
            problems.Add("SQL statement contains write operations (INSERT, UPDATE, DELETE, etc.)");
        }

        // Check for dangerous keywords
        if (ContainsDangerousKeywords(trimmedSql))
        {
            problems.Add("SQL statement contains dangerous keywords (DROP, ALTER, etc.)");
        }

        // Check for leading/trailing whitespace
        if (sql != trimmedSql)
        {
            problems.Add("SQL statement contains leading or trailing whitespace");
        }

        return problems;
    }

    /// <summary>
    /// Determines whether the supplied SQL is valid for execution.
    /// </summary>
    /// <param name="sql">The SQL to check.</param>
    /// <returns><c>true</c> if no validation problems are reported; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sql"/> is <c>null</c>.</exception>
    public static bool IsValidSql(string sql) => sql is not null && !ValidateSql(sql).Any();

    /// <summary>
    /// Ensures that the supplied SQL is valid for execution.
    /// </summary>
    /// <param name="sql">The SQL statement to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sql"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when the SQL is invalid.</exception>
    public static void EnsureValidSql(string sql)
    {
        ArgumentException.ThrowIfNullOrEmpty(sql, nameof(sql));

        var problems = ValidateSql(sql);
        if (problems.Count > EmptyCount)
        {
            var message = $"SQL statement is invalid: {string.Join("; ", problems)}";
            throw new ArgumentException(message, nameof(sql));
        }
    }

    /// <summary>
    /// Checks if an identifier contains SQL injection patterns.
    /// </summary>
    private static bool IsSqlInjectionAttempt(string identifier)
    {
        var upper = identifier.ToUpperInvariant();

        // Check for common SQL injection patterns
        return (upper.Contains(SelectKeyword) && !upper.StartsWith(SelectKeyword))
            || upper.Contains(InsertKeyword)
            || upper.Contains(UpdateKeyword)
            || upper.Contains(DeleteKeyword)
            || upper.Contains(DropKeyword)
            || upper.Contains(AlterKeyword)
            || upper.Contains(CreateKeyword)
            || upper.Contains(TruncateKeyword)
            || upper.Contains(ReplaceKeyword)
            || upper.Contains(ExecuteMarker)
            || upper.Contains(LineCommentMarker)
            || upper.Contains(StatementSeparatorMarker)
            || upper.Contains(BlockCommentStartMarker)
            || upper.Contains(BlockCommentEndMarker)
            || upper.Contains(UnionKeyword)
            || upper.Contains(OrExpressionMarker)
            || upper.Contains(AndExpressionMarker)
            || upper.Contains(ExtendedProcedureMarker)
            || upper.Contains(WaitForMarker)
            || upper.Contains(ShutdownMarker);
    }

    /// <summary>
    /// Checks if an identifier is malformed (contains quotes, brackets, or invalid characters).
    /// </summary>
    private static bool IsMalformedIdentifier(string identifier)
    {
        // Check for embedded double quotes (SQLite uses backticks or brackets for escaping, not double quotes)
        if (identifier.Contains('"'))
        {
            return true;
        }

        // Check for square brackets (SQL Server style, not valid in SQLite)
        if (identifier.Contains('[') || identifier.Contains(']'))
        {
            return true;
        }

        // Check for backticks (MySQL style, not valid in SQLite)
        if (identifier.Contains('`'))
        {
            return true;
        }

        // Check for control characters or other invalid characters
        foreach (var c in identifier)
        {
            if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a string contains multiple SQL statements.
    /// </summary>
    private static bool ContainsMultipleStatements(string sql)
    {
        // Remove comments first to avoid false positives
        var cleanSql = RemoveComments(sql);

        // Count semicolons that aren't part of string literals
        var inString = false;
        var semicolonCount = 0;

        for (var i = 0; i < cleanSql.Length; i++)
        {
            var c = cleanSql[i];

            if (c == '\'' && (i == 0 || cleanSql[i - 1] != '\\'))
            {
                inString = !inString;
            }
            else if (c == ';' && !inString)
            {
                semicolonCount++;
                if (semicolonCount > MaxStatementSeparators)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if SQL contains write keywords.
    /// </summary>
    private static bool ContainsWriteKeywords(string sql)
    {
        var cleanSql = RemoveComments(sql);
        var upper = cleanSql.ToUpperInvariant();
        return WriteKeywords.Any(keyword => upper.Contains(keyword));
    }

    /// <summary>
    /// Checks if SQL contains dangerous keywords.
    /// </summary>
    private static bool ContainsDangerousKeywords(string sql)
    {
        var cleanSql = RemoveComments(sql);
        var upper = cleanSql.ToUpperInvariant();
        return DangerousKeywords.Any(keyword => upper.Contains(keyword));
    }

    /// <summary>
    /// Checks if a string contains only ASCII characters.
    /// </summary>
    private static bool IsAscii(string value)
    {
        return Encoding.UTF8.GetByteCount(value) == value.Length;
    }

    /// <summary>
    /// Checks if an identifier is a SQLite reserved keyword.
    /// Based on: https://www.sqlite.org/lang_keywords.html
    /// </summary>
    private static bool IsReservedKeyword(string identifier)
    {
        // SQLite 3 reserved keywords (keywords that cannot be used as identifiers without quoting)
        var reservedKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ABORT", "ACTION", "ADD", "AFTER", "ALL", AlterKeyword, "ANALYZE", "AND", "AS", "ASC",
            AttachKeyword, "AUTOINCREMENT", "BEFORE", "BEGIN", "BETWEEN", "BY", "CASCADE", "CASE",
            "CAST", "CHECK", "COLLATE", "COLUMN", "COMMIT", "CONFLICT", "CONSTRAINT", CreateKeyword,
            "CROSS", "CURRENT_DATE", "CURRENT_TIME", "CURRENT_TIMESTAMP", "DATABASE", "DEFAULT",
            "DEFERRABLE", "DEFERRED", DeleteKeyword, "DESC", DetachKeyword, "DISTINCT", DropKeyword, "EACH",
            "ELSE", "END", "ESCAPE", "EXCEPT", "EXCLUSIVE", "EXISTS", "EXPLAIN", "FAIL", "FOR",
            "FOREIGN", "FROM", "FULL", "GLOB", "GROUP", "HAVING", "IF", "IGNORE", "IMMEDIATE",
            "IN", "INDEX", "INDEXED", "INITIALLY", "INNER", InsertKeyword, "INSTEAD", "INTERSECT", "INTO",
            "IS", "ISNULL", "JOIN", "KEY", "LEFT", "LIKE", "LIMIT", "MATCH", "NATURAL", "NO",
            "NOT", "NOTNULL", "NULL", "OF", "OFFSET", "ON", "OR", "ORDER", "OUTER", "PLAN",
            PragmaKeyword, "PRIMARY", "QUERY", "RAISE", "RECURSIVE", "REFERENCES", "REGEXP", "REINDEX",
            "RELEASE", "RENAME", ReplaceKeyword, "RESTRICT", "RIGHT", "ROLLBACK", "ROW", "SAVEPOINT",
            SelectKeyword, "SET", TableKeyword, "TEMP", "TEMPORARY", "THEN", "TO", "TRANSACTION", "TRIGGER",
            UnionKeyword, "UNIQUE", UpdateKeyword, "USING", VacuumKeyword, "VALUES", ViewKeyword, "VIRTUAL", "WHEN",
            "WHERE", "WITH", "WITHOUT"
        };

        return reservedKeywords.Contains(identifier);
    }

    /// <summary>
    /// Removes SQL comments (both -- line comments and /* */ block comments).
    /// </summary>

 private static string RemoveComments(string sql)
    {
        var builder = new StringBuilder(sql.Length);
        var i = 0;

        while (i < sql.Length)
        {
            // Line comment
            if (i + 1 < sql.Length && sql[i] == '-' && sql[i + 1] == '-')
            {
                while (i < sql.Length && sql[i] != '\n')
                {
                    i++;
                }
                continue;
            }

            // Block comment
            if (i + 1 < sql.Length && sql[i] == '/' && sql[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < sql.Length && !(sql[i] == '*' && sql[i + 1] == '/'))
                {
                    i++;
                }
                if (i + 1 < sql.Length)
                {
                    i += 2;
                }
                continue;
            }

            builder.Append(sql[i]);
            i++;
        }

        return builder.ToString();
    }
}
