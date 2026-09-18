# McpSqliteExplorer

Read-only MCP server (stdio) that exposes a single SQLite file to an MCP client: schema listing, guarded SELECT queries, and analysis tools (query plans, ERD, table stats). C# / .NET 10.

## Build

```bash
dotnet build McpSqliteExplorer.slnx
dotnet run --project src/McpSqliteExplorer -- <path-to.db>   # or SQLITE_DB_PATH env var
```

## Test

```bash
dotnet test McpSqliteExplorer.slnx
dotnet test tests/McpSqliteExplorer.Tests --filter "FullyQualifiedName~SqlGuardTests"
```

xunit 2.9 + coverlet. Tests use `TestDatabase` (temp SQLite file seeded per test, deleted on dispose). Known state: the run currently aborts after 22 passing tests with a stack overflow in `SqliteValueConverter.ObjectConverter` (recursive JSON serialize).

## Lint / format

No analyzers or `.editorconfig` configured. Use `dotnet format McpSqliteExplorer.slnx` for whitespace/style. `Nullable` and `ImplicitUsings` are enabled in both projects; keep code warning-free under nullable.

## Layout

- `McpSqliteExplorer.slnx` - solution (XML format); references only `src/` and `tests/` projects.
- `src/McpSqliteExplorer/` - the server.
  - `Program.cs` - entry point: resolves DB path (arg or `SQLITE_DB_PATH`), builds `SqliteExplorer`, registers MCP tools via `WithToolsFromAssembly()`. Logs go to stderr only (stdout is the MCP stream).
  - `SqliteExplorer.cs` + partials `SqliteExplorerSchema.cs`, `SqliteExplorerAnalysis.cs` - read-only facade over one DB file (`Mode=ReadOnly`); implements `ISqliteCatalog`.
  - `SqliteExplorerValidation.cs` - identifier and SQL guards (SELECT-only, injection checks); throws `SqliteExplorerValidationException`.
  - `SqliteTools.cs`, `SqliteAnalysisTools.cs` - `[McpServerToolType]` classes; each `[McpServerTool(Name="snake_case")]` static method wraps the explorer and returns a JSON string.
  - `ISqliteCatalog.cs`, `SqliteCatalog.cs` - catalog abstraction plus `MemoizingSqliteCatalog` cache.
  - `SqliteValueConverter.cs`, `SharedJsonExtensions.cs`, `*JsonExtensions.cs`, `*Extensions.cs` - JSON shaping and helper extension methods.
  - `*.cs.backup` - stale copies, not compiled; do not edit.
- `tests/McpSqliteExplorer.Tests/` - xunit tests; has `InternalsVisibleTo` access to internals.
- `docs/` - generated per-class markdown docs.
- Root-level stray files (`SqliteExplorer.cs`, `SqliteExplorerSchema.cs`, `ISqliteConnectionFactory.cs`, `SqliteConnectionFactory.cs`, `SqliteExplorer/`, `src/McpSqliteExplorer.Tests/`, `commit.txt`, the file named `string firstUserName = ...`) are leftovers from automated edits and are not part of the solution. Do not treat them as source of truth.
- `task-factory/aider_buildcmd.py` - helper that runs `dotnet build` for the task-factory automation.

## Conventions

- Namespace `McpSqliteExplorer` (tests: `McpSqliteExplorer.Tests`); file-scoped namespaces, explicit `using` lines at top even with implicit usings.
- Classes are `sealed`; large ones split with `partial` by concern (`SqliteExplorer` + `SqliteExplorerSchema` + `SqliteExplorerAnalysis`).
- Data shapes are `public sealed record` positional records (`TableInfo`, `ColumnInfo`, `QueryResult`, `IndexInfo`, `TableStats`, ...), declared at the bottom of the file that produces them.
- MCP tools: static methods, first parameter `SqliteExplorer explorer` (DI-injected), `[Description]` on the method and every parameter, body wrapped in `Guarded(() => ...)` which converts exceptions into a JSON error payload. Tool names are `snake_case`; C# methods are `PascalCase`.
- All DB access is read-only; any new query path must go through the validation layer (`SqliteExplorerValidation`) before reaching the engine.
- Private fields `_camelCase`; static readonly logger delegates via `LoggerMessage.Define`.
- XML doc comments on all public members.
- Tests: one class per production class or concern (`<Class>Tests`, `<Class><Concern>Tests`), `[Fact]` methods, arrange with `using var db = new TestDatabase();`. Helper extension classes live beside tests (`SqliteExplorerTestsExtensions`, `SqlGuardTestsExtensions`).
- Commit messages: conventional prefixes (`docs:`, `chore:`, `fix:`).
