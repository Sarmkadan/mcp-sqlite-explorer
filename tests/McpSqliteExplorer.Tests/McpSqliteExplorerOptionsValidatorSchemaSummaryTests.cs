using System.Linq;

namespace McpSqliteExplorer.Tests;

/// <summary>
/// Validation rules for the <c>SchemaSummary</c> subsection of <see cref="McpSqliteExplorerOptions"/>.
/// </summary>
public sealed class McpSqliteExplorerOptionsValidatorSchemaSummaryTests
{
    private readonly McpSqliteExplorerOptionsValidator _validator = new();

    private static McpSqliteExplorerOptions OptionsFor(string databasePath, Action<SchemaSummaryOptions> configure)
    {
        var options = new McpSqliteExplorerOptions { DatabasePath = databasePath };
        configure(options.SchemaSummary);
        return options;
    }

    [Fact]
    public void Validate_WithDefaultSchemaSummary_Succeeds()
    {
        using var db = new TestDatabase();

        var result = _validator.Validate(null, new McpSqliteExplorerOptions { DatabasePath = db.Path });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithValidIncludeAndExcludePatterns_Succeeds()
    {
        using var db = new TestDatabase();

        var result = _validator.Validate(null, OptionsFor(db.Path, summary =>
        {
            summary.IncludeTables = ["^(authors|books)$", "loan.*"];
            summary.ExcludeTables = ["recent_.*"];
        }));

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithBlankIncludePattern_Fails(string pattern)
    {
        using var db = new TestDatabase();

        var result = _validator.Validate(null, OptionsFor(db.Path, summary => summary.IncludeTables = [pattern]));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("SchemaSummary:IncludeTables[0] must not be blank"));
    }

    [Theory]
    [InlineData("[")]
    [InlineData("(abc")]
    [InlineData("abc)")]
    [InlineData("a)(b")]
    [InlineData("trailing\\")]
    public void Validate_WithInvalidIncludePattern_Fails(string pattern)
    {
        using var db = new TestDatabase();

        var result = _validator.Validate(null, OptionsFor(db.Path, summary => summary.IncludeTables = [pattern]));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f =>
            f.Contains("SchemaSummary:IncludeTables[0]") && f.Contains("is not a valid regular expression"));
    }

    [Theory]
    [InlineData("[")]
    [InlineData("a)(b")]
    public void Validate_WithInvalidExcludePattern_Fails(string pattern)
    {
        using var db = new TestDatabase();

        var result = _validator.Validate(null, OptionsFor(db.Path, summary => summary.ExcludeTables = ["books", pattern]));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f =>
            f.Contains("SchemaSummary:ExcludeTables[1]") && f.Contains("is not a valid regular expression"));
    }

    [Fact]
    public void Validate_WhenTableIsIncludedAndExcluded_Fails()
    {
        using var db = new TempDatabaseWithTables(("authors", 1), ("tmp_old", 1), ("tmp_keep", 1));

        var result = _validator.Validate(null, OptionsFor(db.Path, summary =>
        {
            summary.IncludeTables = ["tmp_.*"];
            summary.ExcludeTables = ["tmp_old"];
        }));

        Assert.False(result.Succeeded);
        var failure = Assert.Single(result.Failures!);
        Assert.Contains("matched by both IncludeTables and ExcludeTables", failure);
        Assert.Contains("'tmp_old'", failure);
        Assert.DoesNotContain("'tmp_keep'", failure);
    }

    [Fact]
    public void Validate_WhenIncludeAndExcludeNameTheSameTable_Fails()
    {
        using var db = new TempDatabaseWithTables(("books", 1));

        var result = _validator.Validate(null, OptionsFor(db.Path, summary =>
        {
            summary.IncludeTables = ["books"];
            summary.ExcludeTables = ["books"];
        }));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("'books'"));
    }

    [Fact]
    public void Validate_WhenConflictDiffersOnlyInCase_Fails()
    {
        using var db = new TempDatabaseWithTables(("Books", 1));

        var result = _validator.Validate(null, OptionsFor(db.Path, summary =>
        {
            summary.IncludeTables = ["BOOKS"];
            summary.ExcludeTables = ["books"];
        }));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("matched by both"));
    }

    [Fact]
    public void Validate_WhenIncludeAndExcludeDoNotOverlap_Succeeds()
    {
        using var db = new TempDatabaseWithTables(("books", 1), ("loans", 1));

        var result = _validator.Validate(null, OptionsFor(db.Path, summary =>
        {
            summary.IncludeTables = ["books"];
            summary.ExcludeTables = ["loans"];
        }));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithExcludeOnlyAndNoIncludePatterns_Succeeds()
    {
        using var db = new TempDatabaseWithTables(("books", 1));

        // With no include patterns every table is selected, so an exclude pattern cannot conflict with one.
        var result = _validator.Validate(null, OptionsFor(db.Path, summary => summary.ExcludeTables = ["books"]));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenDatabaseIsMissing_SkipsConflictCheck()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.db");

        var result = _validator.Validate(null, new McpSqliteExplorerOptions
        {
            DatabasePath = missingPath,
            AllowMissingDatabase = true,
            SchemaSummary = new SchemaSummaryOptions
            {
                IncludeTables = ["books"],
                ExcludeTables = ["books"],
            },
        });

        // No table names are known, so the conflict cannot be checked and the file-based rule passes.
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenPatternsAreInvalid_SkipsConflictCheckButReportsPatterns()
    {
        using var db = new TempDatabaseWithTables(("books", 1));

        var result = _validator.Validate(null, OptionsFor(db.Path, summary =>
        {
            summary.IncludeTables = ["["];
            summary.ExcludeTables = ["books"];
        }));

        Assert.False(result.Succeeded);
        var failure = Assert.Single(result.Failures!);
        Assert.Contains("SchemaSummary:IncludeTables[0]", failure);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Validate_WithNonNegativeMaxCountedTables_Succeeds(int maxCountedTables)
    {
        using var db = new TestDatabase();

        var result = _validator.Validate(null, OptionsFor(db.Path, summary => summary.MaxCountedTables = maxCountedTables));

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Validate_WithNegativeMaxCountedTables_Fails(int maxCountedTables)
    {
        using var db = new TestDatabase();

        var result = _validator.Validate(null, OptionsFor(db.Path, summary => summary.MaxCountedTables = maxCountedTables));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("SchemaSummary:MaxCountedTables must not be negative"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1000)]
    public void Validate_WithSampleRowsInRange_Succeeds(int sampleRows)
    {
        using var db = new TestDatabase();

        var result = _validator.Validate(null, OptionsFor(db.Path, summary => summary.SampleRows = sampleRows));

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1001)]
    public void Validate_WithSampleRowsOutOfRange_Fails(int sampleRows)
    {
        using var db = new TestDatabase();

        var result = _validator.Validate(null, OptionsFor(db.Path, summary => summary.SampleRows = sampleRows));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("SchemaSummary:SampleRows must be between 0 and 1000"));
    }

    [Fact]
    public void Validate_WithEverySchemaSummaryViolation_ReportsAllFailures()
    {
        using var db = new TempDatabaseWithTables(("books", 1));

        var result = _validator.Validate(null, OptionsFor(db.Path, summary =>
        {
            summary.IncludeTables = [""];
            summary.ExcludeTables = ["books", "("];
            summary.MaxCountedTables = -1;
            summary.SampleRows = 5000;
        }));

        Assert.False(result.Succeeded);
        Assert.Equal(4, result.Failures!.Count());
        Assert.Contains(result.Failures!, f => f.Contains("IncludeTables[0] must not be blank"));
        Assert.Contains(result.Failures!, f => f.Contains("ExcludeTables[1]") && f.Contains("not a valid regular expression"));
        Assert.Contains(result.Failures!, f => f.Contains("MaxCountedTables"));
        Assert.Contains(result.Failures!, f => f.Contains("SampleRows"));
    }
}
