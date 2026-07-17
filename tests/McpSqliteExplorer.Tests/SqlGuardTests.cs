namespace McpSqliteExplorer.Tests;

/// <summary>
/// Tests for the SqliteExplorer's SELECT-only guard.
/// </summary>
public sealed class SqlGuardTests
{
    /// <summary>
    /// Verifies that the SELECT-only guard allows read statements.
    /// </summary>
    /// <param name="sql">The SQL statement to test.</param>
    [Theory]
    [InlineData("SELECT * FROM t")]
    [InlineData("select id from t where x = 1")]
    [InlineData("WITH cte AS (SELECT 1) SELECT * FROM cte")]
    [InlineData("SELECT * FROM t; ")]
    [InlineData("-- a comment\nSELECT 1")]
    [InlineData("/* block */ SELECT 1")]
    public void GuardSelectOnly_AllowsReadStatements(string sql)
    {
        // Should not throw.
        SqliteExplorer.GuardSelectOnly(sql);
    }

    /// <summary>
    /// Verifies that the SELECT-only guard rejects write statements.
    /// </summary>
    /// <param name="sql">The SQL statement to test.</param>
    [Theory]
    [InlineData("INSERT INTO t VALUES (1)")]
    [InlineData("UPDATE t SET x = 1")]
    [InlineData("DELETE FROM t")]
    [InlineData("DROP TABLE t")]
    [InlineData("ALTER TABLE t ADD COLUMN c TEXT")]
    [InlineData("CREATE TABLE t (id INTEGER)")]
    [InlineData("PRAGMA writable_schema = 1")]
    [InlineData("VACUUM")]
    [InlineData("ATTACH DATABASE 'x.db' AS x")]
    public void GuardSelectOnly_RejectsWriteStatements(string sql)
    {
        Assert.Throws<ArgumentException>(() => SqliteExplorer.GuardSelectOnly(sql));
    }

    /// <summary>
    /// Verifies that the SELECT-only guard rejects multiple statements.
    /// </summary>
    /// <param name="sql">The SQL statement to test.</param>
    [Theory]
    [InlineData("SELECT 1; DROP TABLE t")]
    [InlineData("SELECT 1; SELECT 2")]
    public void GuardSelectOnly_RejectsMultipleStatements(string sql)
    {
        Assert.Throws<ArgumentException>(() => SqliteExplorer.GuardSelectOnly(sql));
    }

    /// <summary>
    /// Verifies that the SELECT-only guard rejects write statements hidden behind a CTE.
    /// </summary>
    public void GuardSelectOnly_RejectsWriteHiddenBehindCte()
    {
        Assert.Throws<ArgumentException>(() =>
            SqliteExplorer.GuardSelectOnly("WITH x AS (SELECT 1) DELETE FROM t"));
    }

    /// <summary>
    /// Verifies that the SELECT-only guard rejects empty statements.
    /// </summary>
    /// <param name="sql">The SQL statement to test.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-- only a comment")]
    public void GuardSelectOnly_RejectsEmpty(string sql)
    {
        Assert.Throws<ArgumentException>(() => SqliteExplorer.GuardSelectOnly(sql));
    }

    /// <summary>
    /// Verifies that the ClampLimit method bounds the row cap.
    /// </summary>
    /// <param name="input">The input value to test.</param>
    /// <param name="expected">The expected output value.</param>
    [Theory]
    [InlineData(0, SqliteExplorer.DefaultRowCap)]
    [InlineData(-5, SqliteExplorer.DefaultRowCap)]
    [InlineData(50, 50)]
    [InlineData(5000, SqliteExplorer.MaxRowCap)]
    public void ClampLimit_BoundsTheRowCap(int input, int expected)
    {
        Assert.Equal(expected, SqliteExplorer.ClampLimit(input));
    }
}
