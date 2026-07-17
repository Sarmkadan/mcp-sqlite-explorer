# SqlGuardTestsExtensions

Provides curated collections of SQL statement strings and associated metadata used to validate the behavior of `SqlGuard` and related components. Each static member returns a read‑only list that targets a specific category of input—allowed reads, rejected writes, CTE‑based write attempts, limit‑clamping scenarios, empty inputs, and multi‑statement batches—so that test suites can exercise guard logic consistently without duplicating test data.

## API

### `GetAllowedReadStatements`
```csharp
public static IReadOnlyList<string> GetAllowedReadStatements { get; }
```
**Purpose**  
Returns a list of SQL statements that the guard is expected to classify as safe read operations. These typically include `SELECT` queries, `EXPLAIN` variants, and `PRAGMA` statements that do not modify the database.

**Return value**  
`IReadOnlyList<string>` – a read‑only collection of valid read‑only SQL strings.

**Exceptions**  
None. The property is a pure data accessor.

---

### `GetRejectedWriteStatements`
```csharp
public static IReadOnlyList<string> GetRejectedWriteStatements { get; }
```
**Purpose**  
Returns a list of SQL statements that the guard must reject because they perform write operations (`INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, etc.).

**Return value**  
`IReadOnlyList<string>` – a read‑only collection of write‑oriented SQL strings that should be denied.

**Exceptions**  
None.

---

### `GetRejectedCteWriteStatements`
```csharp
public static IReadOnlyList<string> GetRejectedCteWriteStatements { get; }
```
**Purpose**  
Returns a list of SQL statements that embed write operations inside Common Table Expressions (CTEs). These are used to verify that the guard inspects CTE bodies and does not allow writes disguised through `WITH` clauses.

**Return value**  
`IReadOnlyList<string>` – a read‑only collection of CTE‑based SQL strings that should be rejected.

**Exceptions**  
None.

---

### `GetLimitStatements`
```csharp
public static IReadOnlyList<(string Statement, int OriginalLimit, int ExpectedClampedLimit)> GetLimitStatements { get; }
```
**Purpose**  
Returns a list of tuples where each entry contains a SQL statement, the original `LIMIT` value present in the statement, and the expected clamped limit value after the guard applies its limit‑clamping policy. This supports testing that the guard correctly adjusts or enforces maximum row limits.

**Return value**  
`IReadOnlyList<(string Statement, int OriginalLimit, int ExpectedClampedLimit)>` – a read‑only collection of tuples pairing a SQL string with its original and expected clamped limit values.

**Exceptions**  
None.

---

### `GetEmptyStatements`
```csharp
public static IReadOnlyList<string> GetEmptyStatements { get; }
```
**Purpose**  
Returns a list of empty, whitespace‑only, or null‑equivalent SQL strings. These are used to verify that the guard handles missing or blank input gracefully (e.g., by rejecting or ignoring it without throwing).

**Return value**  
`IReadOnlyList<string>` – a read‑only collection of empty or whitespace SQL strings.

**Exceptions**  
None.

---

### `GetMultiStatements`
```csharp
public static IReadOnlyList<string> GetMultiStatements { get; }
```
**Purpose**  
Returns a list of SQL strings that contain multiple semicolon‑separated statements. These are used to confirm that the guard either rejects multi‑statement batches or processes them according to its security policy (for example, by inspecting every statement in the batch).

**Return value**  
`IReadOnlyList<string>` – a read‑only collection of multi‑statement SQL strings.

**Exceptions**  
None.

## Usage

### Example 1: Parameterized test for allowed read statements
```csharp
[Test]
public void Guard_Accepts_AllAllowedReadStatements(
    [ValueSource(nameof(AllowedReadSource))] string sql)
{
    var guard = new SqlGuard(maxLimit: 100);
    Assert.DoesNotThrow(() => guard.Validate(sql));
}

private static IEnumerable<string> AllowedReadSource()
    => SqlGuardTestsExtensions.GetAllowedReadStatements;
```

### Example 2: Verifying limit clamping across multiple statements
```csharp
[Test]
public void Guard_ClampsLimits_ToConfiguredMaximum()
{
    var guard = new SqlGuard(maxLimit: 50);

    foreach (var (statement, originalLimit, expectedClamped) 
             in SqlGuardTestsExtensions.GetLimitStatements)
    {
        string clamped = guard.ClampLimit(statement);
        Assert.That(clamped, Does.Contain($"LIMIT {expectedClamped}"),
            $"Statement with original LIMIT {originalLimit} was not clamped correctly.");
    }
}
```

## Notes

- All properties return **immutable** collections. The underlying data is instantiated once and shared across callers; no defensive copying is necessary in tests.
- The collections are **thread‑safe for reading**. Multiple test threads can enumerate them concurrently without external synchronization.
- The lists are intentionally **not modifiable**—any attempt to cast and modify will throw at runtime. This preserves the integrity of shared test data.
- `GetEmptyStatements` may include strings that are `null` or consist solely of whitespace. Consumers should ensure their guard implementations handle these without throwing `NullReferenceException` or unintended rejections.
- `GetRejectedCteWriteStatements` is distinct from `GetRejectedWriteStatements` to isolate CTE‑based write detection. Tests should use both to confirm that write detection is not bypassed through CTE syntax.
- The `OriginalLimit` and `ExpectedClampedLimit` values in `GetLimitStatements` assume a specific clamping policy (e.g., a configurable maximum limit). If the guard’s policy changes, these expected values must be updated accordingly.
