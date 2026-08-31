using McpSqliteExplorer;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// The database path comes from the first CLI argument or the SQLITE_DB_PATH
// environment variable. Nothing else is configurable - this server does one
// thing: expose a single SQLite file to an MCP client, read-only.
var dbPath = args.FirstOrDefault() ?? Environment.GetEnvironmentVariable("SQLITE_DB_PATH");

if (string.IsNullOrWhiteSpace(dbPath))
{
    await Console.Error.WriteLineAsync(
        "usage: McpSqliteExplorer <path-to-sqlite.db>  (or set SQLITE_DB_PATH)");
    return 1;
}

if (!File.Exists(dbPath))
{
    await Console.Error.WriteLineAsync($"database file not found: {dbPath}");
    return 1;
}

SqliteExplorer explorer;
try
{
    explorer = new SqliteExplorer(dbPath);
}
catch (ArgumentException ex)
{
    await Console.Error.WriteLineAsync($"invalid database path: {ex.Message}");
    return 1;
}
catch (SqliteException ex)
{
    await Console.Error.WriteLineAsync($"unable to open database: {ex.Message}");
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);

// MCP talks over stdio, so anything written to stdout that is not a protocol
// frame corrupts the stream. Route logs to stderr only.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton(explorer);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

try
{
    await builder.Build().RunAsync();
    return 0;
}
finally
{
    explorer.Dispose();
}
