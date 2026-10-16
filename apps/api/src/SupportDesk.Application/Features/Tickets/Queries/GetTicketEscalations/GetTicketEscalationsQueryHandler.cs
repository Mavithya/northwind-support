using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;

public sealed class GetTicketEscalationsQueryHandler(
    ITicketRepository tickets,
    ITicketEscalationRepository escalations)
{
    public async Task<IReadOnlyList<TicketEscalationDto>> HandleAsync(
        int ticketId,
        CancellationToken ct)
    {
        if (await tickets.GetByIdAsync(ticketId, ct) is null)
        {
            throw new NotFoundException("Ticket", ticketId);
        }

        var history = await escalations.GetByTicketIdAsync(ticketId, ct);

        return history.Select(e => new TicketEscalationDto(
            e.Id,
            e.TicketId,
            e.FromPriority,
            e.ToPriority,
            e.FromAgentId,
            e.ToAgentId,
            e.FromDueAtUtc,
            e.ToDueAtUtc,
            e.Reason,
            e.EscalatedBy,
            e.EscalatedAtUtc)).ToArray();
    }
}