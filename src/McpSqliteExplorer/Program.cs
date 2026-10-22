using McpSqliteExplorer;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// The database path comes from the first CLI argument or the SQLITE_DB_PATH
// environment variable; everything else is read from the McpSqliteExplorer
// configuration section (e.g. McpSqliteExplorer__MaxRows). The options are
// validated when the host starts, so every problem is reported at once.
var builder = Host.CreateApplicationBuilder(args);

var dbPath = args.FirstOrDefault() ?? Environment.GetEnvironmentVariable("SQLITE_DB_PATH");
if (dbPath is not null)
{
    builder.Configuration[$"{McpSqliteExplorerOptions.SectionName}:DatabasePath"] = dbPath;
}

// MCP talks over stdio, so anything written to stdout that is not a protocol
// frame corrupts the stream. Route logs to stderr only.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

// Registered before the MCP server so its validation hosted service starts first:
// an invalid configuration aborts startup before the stdio transport begins.
builder.Services.AddMcpSqliteExplorerOptions();

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<McpSqliteExplorerOptions>>().Value;
    return new SqliteExplorer(options.DatabasePath)
    {
        QueryTimeoutSeconds = (int)options.QueryTimeout.TotalSeconds,
    };
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

using var host = builder.Build();

try
{
    await host.StartAsync();

    // Open the database now so an unreadable file fails at startup, not on the first tool call.
    host.Services.GetRequiredService<SqliteExplorer>();
}
catch (OptionsValidationException ex)
{
    await Console.Error.WriteLineAsync($"invalid configuration ({ex.Failures.Count()} problem(s)):");
    foreach (var failure in ex.Failures)
    {
        await Console.Error.WriteLineAsync($"  - {failure}");
    }

    return 1;
}
catch (FileNotFoundException ex)
{
    await Console.Error.WriteLineAsync($"database file not found: {ex.Message}");
    return 1;
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

await host.WaitForShutdownAsync();
return 0;
