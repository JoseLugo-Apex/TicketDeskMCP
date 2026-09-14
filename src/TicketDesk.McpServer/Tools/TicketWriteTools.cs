using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using TicketDesk.McpServer.Data;

namespace TicketDesk.McpServer.Tools;

using McpServer = ModelContextProtocol.Server.McpServer;

// A tool that stops mid-call to ask the human, using Multi Round-Trip Requests (MRTR)
//
// The older way to ask was to hold the session open and push a request down to the client,
// which makes the server stateful. Here the tool THROWS InputRequiredException carrying the
// question plus an opaque requestState, and the call ends. The client asks the user, then
// re-issues the identical tools/call with the answer in inputResponses and the requestState
// echoed back. Nothing is held open in between, so the retry may even land on a different
// instance — which is what keeps a server like this horizontally scalable.
//
// The method below is three branches, one per case: the reason just came back from the user,
// we still need to ask, or the client cannot be asked. A closeReason supplied by the model is
// never treated as confirmation — it only pre-fills the question put to the human.

[McpServerToolType]
public static class TicketWriteTools
{
    [McpServerTool(Name = "close_ticket"),
     Description("Closes a ticket, recording why it was closed. This modifies the board, so the caller "
               + "will be asked to confirm the reason before the change is applied.")]
    public static string CloseTicket(
        // These first two are injected by the SDK and are invisible to the model: only the
        // parameters it does not recognise (ticketId, closeReason) become the tool's schema.
        // Any DI-registered service can be injected the same way.
        McpServer server,
        RequestContext<CallToolRequestParams> context,
        [Description("The id of the ticket to close, e.g. 4131.")] long ticketId,
        [Description("Suggested reason for closing. The user confirms or edits it before the ticket is closed.")] string? closeReason = null)
    {
        var ticket = TicketStore.Find(ticketId);
        if (ticket is null) return $"No ticket found with id {ticketId}.";
        if (ticket.Status == "closed") return $"Ticket {ticketId} is already closed ({ticket.CloseReason}).";

        const string defaultCloseReason = "completed";

        string Close(string reason) =>
            TicketStore.Close(ticketId, reason)
                ? $"Closed ticket {ticketId} ({ticket.Title}) — reason: {reason}"
                : $"Could not close ticket {ticketId}.";

        // MRTR round trip: the client is re-issuing the same call with the user's answer attached.
        // This is the only path where the change is applied with a human-confirmed reason.
        if (context.Params?.InputResponses?.TryGetValue("closeReason", out var response) is true)
        {
            var elicited = response.Deserialize(InputResponse.ElicitResultJsonTypeInfo);

            // The user can decline or cancel — respect that and leave the ticket open.
            if (elicited?.IsAccepted is not true)
                return $"Cancelled. Ticket {ticketId} is still {ticket.Status}.";

            var confirmedReason = elicited.Content?.TryGetValue("closeReason", out var value) is true
                ? value.GetString()
                : null;

            return Close(string.IsNullOrWhiteSpace(confirmedReason) ? defaultCloseReason : confirmedReason);
        }

        // First pass: stop the call and ask the human, even if the model already proposed a
        // reason. IsMrtrSupported is what the client advertised on connect, so this branch is
        // a capability check, not a version check.
        if (server.IsMrtrSupported)
        {
            throw new InputRequiredException(
                inputRequests: new Dictionary<string, InputRequest>
                {
                    ["closeReason"] = InputRequest.ForElicitation(new ElicitRequestParams
                    {
                        Message = $"Close ticket {ticketId} — \"{ticket.Title}\"? "
                                + "Accept the default reason or write your own.",

                        // We describe the question with a schema; the client renders the
                        // dialog. The server never ships any UI.
                        RequestedSchema = new()
                        {
                            Properties =
                            {
                                ["closeReason"] = new ElicitRequestParams.StringSchema
                                {
                                    Title = "Close reason",
                                    Description = "Why this ticket is being closed",
                                    Default = string.IsNullOrWhiteSpace(closeReason) ? defaultCloseReason : closeReason,
                                },
                            },
                        },
                    })
                },
                requestState: ticketId.ToString());
        }

        // Client cannot be asked: degrade to something the model can still act on, rather
        // than failing. You do not get to choose which clients call your server. The client's
        // own tool-approval prompt is the only confirmation left on this path.
        if (!string.IsNullOrWhiteSpace(closeReason))
            return Close(closeReason);

        return "Closing a ticket requires a reason. Call close_ticket again with a closeReason argument.";
    }
}
