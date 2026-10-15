
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

public sealed record TicketTriageDecision(
    TicketPriority Priority,
    int? AssignedAgentId,
    DateTime DueAtUtc,
    double SlaWindowHours,
    string PriorityReason,
    string SlaReason,
    string AssignmentReason);

/// <summary>
/// Determines the priority, SLA deadline and eligible owner of a new ticket.
/// </summary>
public sealed class TicketTriageService(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentRepository agents,
    IClock clock,
    TicketTriageOptions options)
{
    public async Task<TicketTriageDecision> DecideAsync(
        int customerId,
        int categoryId,
        TicketPriority? requestedPriority,
        CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(customerId, ct)
            ?? throw new NotFoundException("Customer", customerId);

        var category = await categories.GetByIdAsync(categoryId, ct)
            ?? throw new NotFoundException("Category", categoryId);

        var priority = category.ForcesCriticalPriority
            ? TicketPriority.Critical
            : requestedPriority ?? TicketPriority.Medium;

        var priorityReason = category.ForcesCriticalPriority
            ? $"The {category.Name} category requires Critical priority."
            : requestedPriority is null
                ? "No priority was requested, so Medium priority was selected."
                : $"The requested {priority} priority was retained.";

        if (!options.BaseWindowHours.TryGetValue(priority.ToString(), out var baseHours)
            || baseHours <= 0)
        {
            throw new InvalidOperationException(
                $"A positive SLA window must be configured for {priority} priority.");
        }

        if (options.PremiumCustomerMultiplier <= 0
            || options.MinimumWindowHours <= 0)
        {
            throw new InvalidOperationException(
                "The Premium multiplier and minimum SLA window must be positive.");
        }

        var windowHours = customer.Tier == CustomerTier.Premium
            ? Math.Max(
                baseHours * options.PremiumCustomerMultiplier,
                options.MinimumWindowHours)
            : baseHours;

        var now = clock.UtcNow;
        var dueAtUtc = now.AddHours(windowHours);

        var slaReason = customer.Tier == CustomerTier.Premium
            ? $"Base window {baseHours} hours; Premium multiplier "
                + $"{options.PremiumCustomerMultiplier}; effective window "
                + $"{windowHours} hours, including the minimum-window rule."
            : $"The {priority} priority has a {windowHours}-hour SLA window "
                + $"for a Standard customer.";

        var openCounts = await tickets.GetOpenTicketCountsByAgentAsync(ct);
        var allAgents = await agents.GetAllForAssignmentAsync(ct);

        var selected = allAgents
            .Select(agent => new
            {
                Agent = agent,
                OpenCount = openCounts.TryGetValue(agent.Id, out var count)
                    ? count
                    : 0
            })
            .Where(x => x.Agent.IsActive)
            .Where(x =>
                !category.RequiresSpecialist
                || x.Agent.Specializations.Any(
                    s => s.CategoryId == category.Id))
            .Where(x => x.OpenCount < x.Agent.MaxOpenTickets)
            .OrderBy(x => x.OpenCount)
            .ThenBy(x => x.Agent.Id)
            .FirstOrDefault();

        var assignmentReason = selected is null
            ? "No active agent with the required specialization and available "
                + "capacity was eligible. The ticket will remain unassigned."
            : $"{selected.Agent.FullName} was selected because they are eligible, "
                + $"have {selected.OpenCount} open tickets and have available capacity.";

        return new TicketTriageDecision(
            priority,
            selected?.Agent.Id,
            dueAtUtc,
            windowHours,
            priorityReason,
            slaReason,
            assignmentReason);
    }
}
