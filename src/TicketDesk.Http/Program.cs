// Same tools, resources and prompts as the stdio server — only the transport differs.
// The HTTP transport is stateless by default in SDK v2: no initialize handshake,
// no Mcp-Session-Id, every request self-contained and safe to load-balance.

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = "ticket-desk-http", Version = "1.0.0" };
    })
    .WithHttpTransport()          // stateless by default in v2
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly()
    .WithPromptsFromAssembly();

var app = builder.Build();

app.MapMcp("/mcp");

app.Run("http://localhost:3001");
