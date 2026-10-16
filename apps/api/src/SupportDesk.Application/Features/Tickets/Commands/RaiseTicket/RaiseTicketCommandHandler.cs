
using Microsoft.Extensions.Logging;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    IUnitOfWork unitOfWork,
    GetTicketQueryHandler getTicket,
    TicketTriageService triage,
    ILogger<RaiseTicketCommandHandler> logger)
{
    public async Task<TicketDetailDto> HandleAsync(
        CreateTicketRequest request,
        CancellationToken ct)
    {
        // Decide priority, SLA deadline and eligible agent.
        var decision = await triage.DecideAsync(
            request.CustomerId,
            request.CategoryId,
            request.RequestedPriority,
            ct);

        // Create the ticket using the final triage decision.
        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            decision.Priority,
            decision.CreatedAtUtc);

        ticket.AssignTo(
            decision.AssignedAgentId,
            decision.CreatedAtUtc);

        ticket.SetDueAtUtc(
            decision.DueAtUtc,
            decision.CreatedAtUtc);

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised. Priority: {Priority}, " +
            "Assigned agent: {AgentId}, Due: {DueAtUtc}.",
            ticket.Reference,
            ticket.Priority,
            ticket.AssignedAgentId,
            ticket.DueAtUtc);

        // Read the saved ticket, then include the explanation.
        var created = await getTicket.HandleAsync(ticket.Id, ct);

        created.Triage = new TicketTriageDto(
            decision.PriorityReason,
            decision.SlaWindowHours,
            decision.SlaReason,
            decision.AssignmentReason);

            

        return created;
    }
}
