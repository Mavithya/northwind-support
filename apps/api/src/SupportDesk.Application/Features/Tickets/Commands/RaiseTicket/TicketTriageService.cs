
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

public sealed record TicketTriageDecision(
    DateTime CreatedAtUtc,
    TicketPriority Priority,
    int? AssignedAgentId,
    DateTime DueAtUtc,
    double SlaWindowHours,
    string PriorityReason,
    string SlaReason,
    string AssignmentReason);

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
                ? "No priority was requested; Medium was selected."
                : $"The requested {priority} priority was retained.";

        var now = clock.UtcNow;
        var (baseHours, windowHours) =
            CalculateWindow(customer.Tier, priority);

        var dueAtUtc = now.AddHours(windowHours);

        var assignment = await SelectAgentAsync(
            category, null, ct);

        return new TicketTriageDecision(
            now,
            priority,
            assignment.Agent?.Id,
            dueAtUtc,
            windowHours,
            priorityReason,
            BuildSlaReason(customer.Tier, baseHours, windowHours),
            assignment.Reason);
    }

    public async Task<TicketTriageDecision> DecideEscalationAsync(
        int customerId,
        int categoryId,
        TicketPriority currentPriority,
        int? currentAgentId,
        CancellationToken ct)
    {
        if (currentPriority == TicketPriority.Critical)
        {
            throw new ConflictException(
                "Critical tickets cannot be escalated.");
        }

        var customer = await customers.GetByIdAsync(customerId, ct)
            ?? throw new NotFoundException("Customer", customerId);

        var category = await categories.GetByIdAsync(categoryId, ct)
            ?? throw new NotFoundException("Category", categoryId);

        var newPriority =
            (TicketPriority)((int)currentPriority + 1);

        var now = clock.UtcNow;
        var (baseHours, windowHours) =
            CalculateWindow(customer.Tier, newPriority);

        var dueAtUtc = now.AddHours(windowHours);

        var assignment = await SelectAgentAsync(
            category, currentAgentId, ct);

        return new TicketTriageDecision(
            now,
            newPriority,
            assignment.Agent?.Id,
            dueAtUtc,
            windowHours,
            $"Priority increased from {currentPriority} to {newPriority}.",
            BuildSlaReason(customer.Tier, baseHours, windowHours),
            assignment.Reason);
    }

    private (double BaseHours, double EffectiveHours) CalculateWindow(
        CustomerTier tier,
        TicketPriority priority)
    {
        if (!options.BaseWindowHours.TryGetValue(
                priority.ToString(), out var baseHours)
            || baseHours <= 0)
        {
            throw new InvalidOperationException(
                $"A valid SLA window must be configured for {priority}.");
        }

        if (options.PremiumCustomerMultiplier <= 0
            || options.MinimumWindowHours <= 0)
        {
            throw new InvalidOperationException(
                "The Premium multiplier and minimum SLA window must be positive.");
        }

        var effectiveHours = tier == CustomerTier.Premium
            ? Math.Max(
                baseHours * options.PremiumCustomerMultiplier,
                options.MinimumWindowHours)
            : baseHours;

        return (baseHours, effectiveHours);
    }

    private static string BuildSlaReason(
        CustomerTier tier,
        double baseHours,
        double effectiveHours) =>
        tier == CustomerTier.Premium
            ? $"Base window {baseHours} hours; Premium multiplier "
                + $"applied with the minimum-window rule; effective "
                + $"window {effectiveHours} hours."
            : $"Base and effective SLA window: {effectiveHours} hours "
                + "for a Standard customer.";

    private async Task<(Agent? Agent, string Reason)> SelectAgentAsync(
        Category category,
        int? currentAgentId,
        CancellationToken ct)
    {
        var openCounts =
            await tickets.GetOpenTicketCountsByAgentAsync(ct);

        var allAgents =
            await agents.GetAllForAssignmentAsync(ct);

        bool HasSpecialization(Agent agent) =>
            !category.RequiresSpecialist
            || agent.Specializations.Any(
                s => s.CategoryId == category.Id);

        // During escalation, preserve the current agent if still eligible.
        // Their count includes the ticket currently being escalated.
        if (currentAgentId.HasValue)
        {
            var current = allAgents.FirstOrDefault(
                a => a.Id == currentAgentId.Value);

            var currentCount =
                openCounts.GetValueOrDefault(currentAgentId.Value);

            if (current is not null
                && current.IsActive
                && HasSpecialization(current)
                && currentCount <= current.MaxOpenTickets)
            {
                return (
                    current,
                    $"Kept {current.FullName}; the agent remains active, "
                    + "qualified and within capacity.");
            }
        }

        var selected = allAgents
            .Select(agent => new
            {
                Agent = agent,
                OpenCount = openCounts.GetValueOrDefault(agent.Id)
            })
            .Where(x => x.Agent.IsActive)
            .Where(x => HasSpecialization(x.Agent))
            .Where(x => x.OpenCount < x.Agent.MaxOpenTickets)
            .OrderBy(x => x.OpenCount)
            .ThenBy(x => x.Agent.Id)
            .FirstOrDefault();

        if (selected is null)
        {
            return (
                null,
                "No active agent with the required specialization and "
                + "available capacity was eligible. The ticket remains unassigned.");
        }

        return (
            selected.Agent,
            $"{selected.Agent.FullName} was selected with "
            + $"{selected.OpenCount} open tickets and available capacity.");
    }
}
