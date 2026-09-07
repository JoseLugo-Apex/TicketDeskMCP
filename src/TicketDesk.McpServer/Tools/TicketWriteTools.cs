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
// The method below is four branches, one per case: the reason was supplied up front, the
// reason just came back from the user, we still need to ask, or the client cannot be asked.

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
        [Description("Why the ticket is being closed.")] string? closeReason = null)
    {
        var ticket = TicketStore.Find(ticketId);
        if (ticket is null) return $"No ticket found with id {ticketId}.";
        if (ticket.Status == "closed") return $"Ticket {ticketId} is already closed ({ticket.CloseReason}).";

        const string defaultCloseReason = "completed";
        var confirmedReason = closeReason;

        // MRTR round trip: the client is re-issuing the same call with the user's answer attached.
        if (string.IsNullOrWhiteSpace(confirmedReason) &&
            context.Params?.InputResponses?.TryGetValue("closeReason", out var response) is true)
        {
            var elicited = response.Deserialize(InputResponse.ElicitResultJsonTypeInfo);

            // The user can decline or cancel — respect that and leave the ticket open.
            if (elicited?.IsAccepted is not true)
                return $"Cancelled. Ticket {ticketId} is still {ticket.Status}.";

            confirmedReason = elicited.Content?.TryGetValue("closeReason", out var value) is true
                ? value.GetString()
                : null;

            confirmedReason = string.IsNullOrWhiteSpace(confirmedReason)
                ? defaultCloseReason
                : confirmedReason;
        }

        // A reason exists — supplied up front, or just collected. Do the work.
        if (!string.IsNullOrWhiteSpace(confirmedReason))
        {
            return TicketStore.Close(ticketId, confirmedReason)
                ? $"Closed ticket {ticketId} ({ticket.Title}) — reason: {confirmedReason}"
                : $"Could not close ticket {ticketId}.";
        }

        // No reason yet: stop the call and ask the human. IsMrtrSupported is what the client
        // advertised on connect, so this branch is a capability check, not a version check.
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
                                    Default = defaultCloseReason,
                                },
                            },
                        },
                    })
                },
                requestState: ticketId.ToString());
        }

        // Client cannot be asked: degrade to something the model can still act on, rather
        // than failing. You do not get to choose which clients call your server.
        return "Closing a ticket requires a reason. Call close_ticket again with a closeReason argument.";
    }
}
