using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using TicketDesk.McpServer.Data;

namespace TicketDesk.McpServer.Resources;

// RESOURCES are user-controlled, and that is the whole difference from a tool. No model
// decides to fetch these: the user picks one out of a list in the client and attaches it to
// the conversation, the way you attach a file. Addressed by URI rather than called by name.
//
// It is the safe way to hand over context you do not want a model pulling on its own —
// and, as with tickets://conventions below, a place to put team policy the model should
// apply instead of inventing its own.

[McpServerResourceType]
public static class TicketResources
{
    [McpServerResource(
        UriTemplate = "tickets://board",
        Name = "ticket_board",
        MimeType = "application/json"),
     Description("A snapshot of the whole team ticket board, as JSON. Attach this when you want the "
               + "model to reason over everything at once instead of searching.")]
    public static string Board() =>
        JsonSerializer.Serialize(TicketStore.All, new JsonSerializerOptions { WriteIndented = true });

    [McpServerResource(
        UriTemplate = "tickets://conventions",
        Name = "team_conventions",
        MimeType = "text/markdown"),
     Description("How this team writes tickets: statuses, priority definitions and the "
               + "definition of done. Useful context when drafting or triaging.")]
    public static string Conventions() =>
        """
        # Ticket conventions

        ## Statuses
        - `open` — accepted, not started
        - `in_progress` — someone is actively on it
        - `blocked` — waiting on something outside the team; must name the blocker
        - `closed` — done, with a close reason

        ## Priority
        - `P1` — production impact for members. Same-day.
        - `P2` — degraded experience or team-blocking. Same sprint.
        - `P3` — everything else.

        ## Definition of done
        A ticket may only be closed with a reason. "Done" is not a reason.
        Anything touching eligibility or claims needs a second reviewer.
        """;
}
