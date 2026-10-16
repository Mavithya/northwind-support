using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ITicketEscalationRepository escalations,
    IUnitOfWork unitOfWork,
    TicketTriageService triage,
    GetTicketQueryHandler getTicket)
{
    public async Task<EscalateTicketResponse> HandleAsync(
        int id,
        EscalateTicketRequest request,
        CancellationToken ct)
    {
        var reason = request.Reason?.Trim();
        var actor = request.EscalatedBy?.Trim();

        var errors = new Dictionary<string, string[]>();

        if (reason is null || reason.Length is < 5 or > 500)
        {
            errors["reason"] =
            [
                "Reason must contain between 5 and 500 characters."
            ];
        }

        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 100)
        {
            errors["escalatedBy"] =
            [
                "An escalation actor is required and cannot exceed 100 characters."
            ];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var ticket = await tickets.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Ticket", id);

        if (ticket.Priority == TicketPriority.Critical)
        {
            throw new ConflictException(
                "Critical tickets cannot be escalated.");
        }

        if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            throw new ConflictException(
                $"Tickets in {ticket.Status} status cannot be escalated.");
        }

        var fromPriority = ticket.Priority;
        var fromAgentId = ticket.AssignedAgentId;
        var fromDueAtUtc = ticket.DueAtUtc;

        var decision = await triage.DecideEscalationAsync(
            ticket.CustomerId,
            ticket.CategoryId,
            ticket.Priority,
            ticket.AssignedAgentId,
            ct);

        ticket.EscalateTo(
            decision.Priority,
            decision.AssignedAgentId,
            decision.DueAtUtc,
            decision.CreatedAtUtc);

        var history = new TicketEscalation(
            ticket.Id,
            fromPriority,
            ticket.Priority,
            fromAgentId,
            ticket.AssignedAgentId,
            fromDueAtUtc,
            ticket.DueAtUtc,
            reason!,
            actor!,
            decision.CreatedAtUtc);

        await escalations.AddAsync(history, ct);

        // Save ticket and history in the same unit of work.
        await unitOfWork.SaveChangesAsync(ct);

        var updatedTicket = await getTicket.HandleAsync(id, ct);

        var historyDto = new TicketEscalationDto(
            history.Id,
            history.TicketId,
            history.FromPriority,
            history.ToPriority,
            history.FromAgentId,
            history.ToAgentId,
            history.FromDueAtUtc,
            history.ToDueAtUtc,
            history.Reason,
            history.EscalatedBy,
            history.EscalatedAtUtc);

        return new EscalateTicketResponse(
            updatedTicket,
            historyDto);
    }
}