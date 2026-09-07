using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using TicketDesk.McpServer.Data;

namespace TicketDesk.McpServer.Tools;

// TOOLS are model-controlled: the model decides when to call one, and the client asks the
// user to approve it.
//
// What the model actually receives for each method below is three things — the tool name,
// the [Description] text, and the JSON schema generated from the parameters. It never sees
// the method body. So the descriptions are not documentation; they are the interface the
// model programs against, and they are worth writing as carefully as any public API.
//
// Note how they cross-reference each other ("use this first", "call this after
// search_tickets"): that is how you encode a call sequence without any orchestration code.

[McpServerToolType]
public static class TicketTools
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    [McpServerTool(Name = "search_tickets"),
     Description("Searches the team's ticket board. Use this first when the user asks what is open, "
               + "what someone is working on, or to find tickets about a topic. All filters are optional; "
               + "with no filters it returns the whole board.")]
    public static string SearchTickets(
        // Parameter descriptions land in the schema too. Spelling out the valid values and
        // giving a worked example is how the model learns them; C# strings carry no enum.
        [Description("Filter by status: open, in_progress, blocked or closed.")] string? status = null,
        [Description("Filter by assignee username, e.g. 'maria'. Use 'unassigned' for unowned work.")] string? assignee = null,
        [Description("Free-text match against the title, summary and component.")] string? query = null)
    {
        var results = TicketStore.Search(status, assignee, query)
            .Select(t => new { t.Id, t.Title, t.Status, t.Priority, t.Assignee, t.Component })
            .ToList();

        return results.Count == 0
            ? "No tickets matched those filters."
            : JsonSerializer.Serialize(results, Json);
    }

    [McpServerTool(Name = "get_ticket"),
     Description("Returns the full detail of a single ticket, including its summary. "
               + "Call this after search_tickets when the user wants to dig into one item.")]
    public static string GetTicket(
        [Description("The numeric ticket id, e.g. 4177.")] long ticketId)
    {
        var ticket = TicketStore.Find(ticketId);
        return ticket is null
            ? $"No ticket found with id {ticketId}."
            : JsonSerializer.Serialize(ticket, Json);
    }

    [McpServerTool(Name = "sprint_health"),
     Description("Returns an aggregate view of the board: counts by status and priority, blocked items, "
               + "and unassigned work. Use this for questions like 'how is the sprint going' or "
               + "'what should we worry about'.")]
    public static string SprintHealth()
    {
        var all = TicketStore.All;

        // The return value is just text to the model, so the key names are part of the
        // prompt: "unassignedOpen" and "openP1" read better to it than a generic DTO dump.
        var summary = new
        {
            total = all.Count,
            byStatus = all.GroupBy(t => t.Status).ToDictionary(g => g.Key, g => g.Count()),
            byPriority = all.GroupBy(t => t.Priority).ToDictionary(g => g.Key, g => g.Count()),
            blocked = all.Where(t => t.Status == "blocked")
                         .Select(t => new { t.Id, t.Title }).ToList(),
            unassignedOpen = all.Where(t => t.Assignee == "unassigned" && t.Status != "closed")
                                .Select(t => new { t.Id, t.Title, t.Priority }).ToList(),
            openP1 = all.Where(t => t.Priority == "P1" && t.Status != "closed")
                        .Select(t => new { t.Id, t.Title, t.Assignee }).ToList(),
        };

        return JsonSerializer.Serialize(summary, Json);
    }
}
