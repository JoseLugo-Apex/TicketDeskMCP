namespace TicketDesk.McpServer.Data;

public record Ticket(
    long Id,
    string Title,
    string Status,        // open | in_progress | blocked | closed
    string Priority,      // P1 | P2 | P3
    string Assignee,
    string Component,
    string Summary)
{
    public string? CloseReason { get; set; }
}

/// <summary>
/// In-memory stand-in for Jira / Azure DevOps / ServiceNow.
/// In a real server this would be a scoped service injected into the tools.
/// </summary>
public static class TicketStore
{
    private static readonly List<Ticket> _tickets =
    [
        new(4181, "Eligibility lookup returns 500 for group plans", "open", "P1", "maria",
            "eligibility-api",
            "Any group plan with more than 200 members fails on the /eligibility/{memberId} endpoint. Started after the Tuesday deploy. No repro on individual plans."),

        new(4177, "Member portal login loop on Safari 18", "in_progress", "P1", "jose",
            "web-portal",
            "Users on Safari 18 are bounced back to the login screen after a successful auth callback. Suspect SameSite cookie handling."),

        new(4165, "Claims search is slow past 10k results", "open", "P2", "unassigned",
            "claims-service",
            "The claims search endpoint takes 8-12s once the result set exceeds 10k rows. Missing index on submitted_date is the leading theory."),

        new(4160, "Add pagination to provider directory", "open", "P3", "ana",
            "web-portal",
            "The provider directory renders all results in one page. Needs server-side pagination and a page-size control."),

        new(4158, "Nightly benefits sync silently skips terminated members", "blocked", "P2", "jose",
            "batch-sync",
            "Terminated members are dropped from the nightly sync without an error. Blocked waiting on the data contract from the upstream team."),

        new(4149, "Upgrade Angular 19 -> 20", "in_progress", "P3", "maria",
            "web-portal",
            "Framework upgrade. Control-flow syntax migration is done; remaining work is the deprecated animations package."),

        new(4131, "Rate-limit the public FAQ endpoint", "closed", "P2", "ana",
            "web-portal",
            "Added a 60 req/min per-IP limit at the gateway.") { CloseReason = "shipped in release 24.6" },

        new(4120, "Fix flaky eligibility integration test", "closed", "P3", "jose",
            "eligibility-api",
            "Test depended on wall-clock time. Replaced with a fixed clock.") { CloseReason = "completed" },
    ];

    public static IReadOnlyList<Ticket> All => _tickets;

    public static Ticket? Find(long id) => _tickets.FirstOrDefault(t => t.Id == id);

    public static IEnumerable<Ticket> Search(string? status, string? assignee, string? query)
    {
        IEnumerable<Ticket> results = _tickets;

        if (!string.IsNullOrWhiteSpace(status))
            results = results.Where(t => t.Status.Equals(status, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(assignee))
            results = results.Where(t => t.Assignee.Equals(assignee, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query))
            results = results.Where(t =>
                t.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                t.Summary.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                t.Component.Contains(query, StringComparison.OrdinalIgnoreCase));

        return results.OrderBy(t => t.Priority).ThenBy(t => t.Id);
    }

    public static bool Close(long id, string reason)
    {
        var ticket = Find(id);
        if (ticket is null || ticket.Status == "closed") return false;

        var closed = ticket with { Status = "closed" };
        closed.CloseReason = reason;

        _tickets[_tickets.IndexOf(ticket)] = closed;
        return true;
    }
}
