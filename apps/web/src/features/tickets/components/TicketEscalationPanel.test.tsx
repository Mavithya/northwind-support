import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import type {
  TicketDetail,
  TicketEscalation,
} from '../../../types/api';
import { TicketEscalationPanel } from './TicketEscalationPanel';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    getTicketEscalations: vi.fn(),
    escalateTicket: vi.fn(),
  },
}));

const getTicketEscalations = vi.mocked(ticketsApi.getTicketEscalations);
const escalateTicket = vi.mocked(ticketsApi.escalateTicket);

const ticket: TicketDetail = {
  id: 1,
  reference: 'SUP-0001',
  title: 'Payment processing issue',
  description: 'Customers are experiencing payment delays.',
  status: 'Open',
  priority: 'Medium',
  customer: {
    id: 2,
    name: 'Alex Morgan',
    email: 'alex@example.com',
    phone: null,
    tier: 'Standard',
  },
  category: {
    id: 3,
    name: 'Payments',
    requiresSpecialist: false,
  },
  assignedAgent: {
    id: 4,
    fullName: 'Sara Lindqvist',
  },
  createdAtUtc: '2026-10-16T08:00:00Z',
  updatedAtUtc: '2026-10-16T08:30:00Z',
  dueAtUtc: '2026-10-17T08:00:00Z',
  resolvedAtUtc: null,
  slaStatus: 'WithinSla',
};

const historyItem: TicketEscalation = {
  id: 10,
  ticketId: 1,
  fromPriority: 'Medium',
  toPriority: 'High',
  fromAgentId: 4,
  toAgentId: 5,
  fromDueAtUtc: '2026-10-17T08:00:00Z',
  toDueAtUtc: '2026-10-16T16:00:00Z',
  reason: 'Customer impact is increasing.',
  escalatedBy: 'Support Agent',
  escalatedAtUtc: '2026-10-16T09:00:00Z',
};

const escalatedTicket: TicketDetail = {
  ...ticket,
  priority: 'High',
  assignedAgent: {
    id: 5,
    fullName: 'New Support Agent',
  },
  dueAtUtc: historyItem.toDueAtUtc,
};

describe('TicketEscalationPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getTicketEscalations.mockResolvedValue([]);
  });

  it('loads and displays escalation history', async () => {
    getTicketEscalations.mockResolvedValue([historyItem]);

    render(
      <TicketEscalationPanel
        ticket={ticket}
        onUpdated={vi.fn()}
      />,
    );

    expect(
      await screen.findByText(historyItem.reason),
    ).toBeInTheDocument();

    expect(
      screen.getByText(/Escalated by:/),
    ).toBeInTheDocument();

    expect(getTicketEscalations).toHaveBeenCalledWith(ticket.id);
  });

  it('submits an escalation and updates the displayed ticket', async () => {
    getTicketEscalations
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([historyItem]);

    escalateTicket.mockResolvedValue({
      ticket: escalatedTicket,
      escalation: historyItem,
    });

    const onUpdated = vi.fn();

    render(
      <TicketEscalationPanel
        ticket={ticket}
        onUpdated={onUpdated}
      />,
    );

    fireEvent.change(screen.getByLabelText('Reason'), {
      target: { value: 'Customer impact is increasing' },
    });

    fireEvent.change(screen.getByLabelText('Escalated by'), {
      target: { value: 'Support Agent' },
    });

    fireEvent.click(
      screen.getByRole('button', { name: 'Escalate ticket' }),
    );

    await waitFor(() => {
      expect(escalateTicket).toHaveBeenCalledWith(ticket.id, {
        reason: 'Customer impact is increasing',
        escalatedBy: 'Support Agent',
      });
    });

    await waitFor(() => {
      expect(onUpdated).toHaveBeenCalledWith(escalatedTicket);
    });

    expect(
      await screen.findByText(historyItem.reason),
    ).toBeInTheDocument();

    expect(getTicketEscalations).toHaveBeenCalledTimes(2);
  });
});