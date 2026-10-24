# Contributing

Thanks for helping with McpSqliteExplorer. This guide covers local setup, the checks CI runs, and what a pull request should contain.

## Prerequisites

- **.NET 10 SDK** (`10.0.x`, the version CI uses). Check with `dotnet --version`.
- Git.

The projects target `net10.0` with `Nullable` and `ImplicitUsings` enabled.

## Build

From the repository root:

```bash
dotnet build McpSqliteExplorer.slnx
```

## Run tests

```bash
dotnet test McpSqliteExplorer.slnx
```

To run a subset, filter by name, for example:

```bash
dotnet test tests/McpSqliteExplorer.Tests --filter "FullyQualifiedName~SqlGuardTests"
```

Tests use xunit and create a temporary SQLite file per test, so no external database is needed.

## Run the server locally

The server speaks MCP over stdio and takes the database path as its first argument or from the `SQLITE_DB_PATH` environment variable. Use an absolute path:

```bash
dotnet build McpSqliteExplorer.slnx
dotnet run --no-build --project src/McpSqliteExplorer -- /absolute/path/to/app.db
```

Keep `--no-build` and build first. A `dotnet run` that compiles writes warnings to stdout, which corrupts the MCP stream. Logs go to stderr only. See the [README](README.md) for client configuration.

## Project layout

- `src/McpSqliteExplorer/` - the server (`Program.cs` entry point, `SqliteExplorer*.cs` read-only facade, `SqliteExplorerValidation.cs` guards, `SqliteTools.cs` and `SqliteAnalysisTools.cs` MCP tools).
- `tests/McpSqliteExplorer.Tests/` - xunit tests.
- `docs/` - generated per-class markdown. Do not edit by hand.
- `*.cs.backup` files are stale copies and are not compiled. Do not edit them. Stray files at the repository root (for example `SqliteExplorer.cs`, `commit.txt`) are not part of the solution either.

## Coding conventions

No analyzers or `.editorconfig` are configured in this repository. Follow the conventions already in the code:

- File-scoped namespaces (`McpSqliteExplorer`, tests use `McpSqliteExplorer.Tests`). Keep explicit `using` lines at the top.
- Classes are `sealed`. Large classes are split with `partial` by concern.
- Data shapes are `public sealed record` types, declared at the bottom of the file that produces them.
- MCP tools are static methods with `[McpServerTool(Name = "snake_case")]`, `[Description]` on the method and each parameter, and a body wrapped in `Guarded(...)`.
- Private fields use `_camelCase`. Public members have XML doc comments.
- Tests follow `<Class>Tests` or `<Class><Concern>Tests`, use `[Fact]`, and start with `using var db = new TestDatabase();`.
- All database access is read-only. Any new query path must go through `SqliteExplorerValidation` before it reaches the engine.
- Format whitespace and style with `dotnet format McpSqliteExplorer.slnx`.

## Branch naming

There is no enforced convention. Use `<type>/<short-kebab-case-description>`, where `<type>` matches the commit prefix, for example `fix/validate-empty-table-name` or `docs/contributing`. Branch from `main`.

## Commit messages

Use conventional prefixes such as `feat:`, `fix:`, `docs:`, `chore:`, and `test:`. Keep the subject short and in the imperative mood.

## Pull request checklist

Before you open a pull request against `main`, confirm that:

- [ ] `dotnet build McpSqliteExplorer.slnx` succeeds.
- [ ] `dotnet test McpSqliteExplorer.slnx` passes.
- [ ] The build adds no new warnings. Compare the warning count with `main`.
- [ ] New or changed behavior has tests, and new query paths go through validation.
- [ ] The description explains what changed and why, and says how you tested it.

CI runs build and test in Release on every pull request to `main`.
