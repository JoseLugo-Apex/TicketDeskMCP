using System.ComponentModel;
using ModelContextProtocol.Server;

namespace TicketDesk.McpServer.Prompts;

// PROMPTS are user-invoked: they show up in the client as something the user triggers (a
// slash command in VS Code), and the returned text is fed to the model as the user's turn.
// Parameters come from the user, not the model.
//
// So this is a reusable, parameterised recipe that lives in the repo and is reviewed in PRs
// instead of in someone's notes. Note that triage_ticket below drives the other primitives:
// it tells the model which tool to call and which resource to read.

[McpServerPromptType]
public static class TicketPrompts
{
    [McpServerPrompt(Name = "triage_ticket"),
     Description("Walks the model through triaging a single ticket: severity, likely cause, "
               + "what to check first, and who should own it.")]
    public static string TriageTicket(
        [Description("The numeric ticket id to triage, e.g. 4181.")] long ticketId) =>
        $"""
        Triage ticket {ticketId}.

        Use get_ticket to load the detail, and read the tickets://conventions resource
        so you apply our priority definitions rather than your own.

        Then give me, in this order and nothing else:
        1. Whether the current priority is right, and why.
        2. The two most likely causes, most likely first.
        3. The first three things I should check, as concrete steps.
        4. Who on the board is the natural owner, based on what they already have in flight.

        Be direct. If the ticket description is missing something you need, say what is missing
        instead of guessing.
        """;

    [McpServerPrompt(Name = "standup_notes"),
     Description("Generates standup notes for one person from the current board state.")]
    public static string StandupNotes(
        [Description("The assignee username, e.g. 'jose'.")] string assignee) =>
        $"""
        Use search_tickets to find everything assigned to {assignee}, then write standup notes
        in three short bullets: what's in progress, what's blocked and on whom, and what's next.
        No preamble, no closing summary. Under 60 words.
        """;
}
