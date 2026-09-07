using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Ticket Desk MCP server, stdio transport. The client (VS Code, Claude Desktop, ...)
// launches this process and talks JSON-RPC over stdin/stdout.
//
// stdout is reserved for those JSON-RPC frames, so all logging goes to stderr instead.
// A stray Console.WriteLine here corrupts the stream and the client drops the connection.

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = "ticket-desk", Version = "1.0.0" };

        // Sent to the client on connect. Most clients drop it into the model's system
        // prompt, so this is where you tell the model how to use the server as a whole.
        options.ServerInstructions =
            "Read-only access to the team's ticket board, plus a guarded action to close a ticket. " +
            "Prefer search_tickets to find work, get_ticket for detail, and sprint_health for status questions.";
    })
    .WithStdioServerTransport()
    // Reflection at startup: each *FromAssembly call scans for its attribute and registers
    // what it finds. Tool parameters become a JSON schema generated from the C# signature —
    // there is no schema to hand-write and nothing to keep in sync.
    .WithToolsFromAssembly()       // [McpServerToolType]
    .WithResourcesFromAssembly()   // [McpServerResourceType]
    .WithPromptsFromAssembly();    // [McpServerPromptType]

await builder.Build().RunAsync();
