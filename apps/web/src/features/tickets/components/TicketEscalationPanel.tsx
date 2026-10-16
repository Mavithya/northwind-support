import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { ErrorMessage } from '../../../components/ErrorMessage';
import { Field } from '../../../components/Field';
import type {
  TicketDetail,
  TicketEscalation,
} from '../../../types/api';
import { formatDateTime } from '../../../lib/format';

interface TicketEscalationPanelProps {
  ticket: TicketDetail;
  onUpdated: (ticket: TicketDetail) => void;
}

function formatAgent(agentId: number | null): string {
  return agentId === null ? 'Unassigned' : `Agent #${agentId}`;
}

export function TicketEscalationPanel({
  ticket,
  onUpdated,
}: TicketEscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [escalatedBy, setEscalatedBy] = useState('');
  const [history, setHistory] = useState<TicketEscalation[]>([]);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isLoadingHistory, setIsLoadingHistory] = useState(true);
  const [formError, setFormError] = useState<string>();
  const [historyError, setHistoryError] = useState<string>();

  const canEscalate =
    ticket.priority !== 'Critical' &&
    ticket.status !== 'Resolved' &&
    ticket.status !== 'Closed';

const loadHistory = useCallback(async () => {
  try {
    const items = await ticketsApi.getTicketEscalations(ticket.id);
    setHistory(items);
    setHistoryError(undefined);
  } catch (caught) {
    setHistoryError(
      toErrorMessage(caught, 'Could not load escalation history.'),
    );
  } finally {
    setIsLoadingHistory(false);
  }
}, [ticket.id]);

useEffect(() => {
  let cancelled = false;

  ticketsApi
    .getTicketEscalations(ticket.id)
    .then((items) => {
      if (cancelled) return;

      setHistory(items);
      setHistoryError(undefined);
    })
    .catch((caught: unknown) => {
      if (cancelled) return;

      setHistoryError(
        toErrorMessage(caught, 'Could not load escalation history.'),
      );
    })
    .finally(() => {
      if (!cancelled) {
        setIsLoadingHistory(false);
      }
    });

  return () => {
    cancelled = true;
  };
}, [ticket.id]);
  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedReason = reason.trim();
    const trimmedActor = escalatedBy.trim();

    if (trimmedReason.length < 5 || trimmedReason.length > 500) {
      setFormError('The reason must contain between 5 and 500 characters.');
      return;
    }

    if (!trimmedActor || trimmedActor.length > 100) {
      setFormError(
        'Enter the name of the person escalating this ticket (maximum 100 characters).',
      );
      return;
    }

    if (!canEscalate) {
      setFormError('This ticket can no longer be escalated.');
      return;
    }

    setIsSubmitting(true);
    setFormError(undefined);

    try {
      const response = await ticketsApi.escalateTicket(ticket.id, {
        reason: trimmedReason,
        escalatedBy: trimmedActor,
      });

      onUpdated(response.ticket);
      setReason('');
      await loadHistory();
    } catch (caught) {
      setFormError(
        toErrorMessage(caught, 'Could not escalate this ticket.'),
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <section className="card">
      <h2>Ticket escalation</h2>

      {canEscalate ? (
        <form onSubmit={handleSubmit}>
          <Field id="escalation-reason" label="Reason">
            {(fieldProps) => (
              <textarea
                {...fieldProps}
                value={reason}
                onChange={(event) => setReason(event.target.value)}
                rows={3}
                minLength={5}
                maxLength={500}
                required
                disabled={isSubmitting}
                placeholder="Explain why this ticket needs escalation"
              />
            )}
          </Field>

          <Field id="escalated-by" label="Escalated by">
            {(fieldProps) => (
              <input
                {...fieldProps}
                value={escalatedBy}
                onChange={(event) => setEscalatedBy(event.target.value)}
                maxLength={100}
                required
                disabled={isSubmitting}
                placeholder="Enter the support agent's name"
              />
            )}
          </Field>

          <p className="field__hint">
            Escalation increases the priority and may update the assignment
            and SLA deadline.
          </p>

          {formError && (
            <ErrorMessage message={formError} />
          )}

          <button type="submit" disabled={isSubmitting}>
            {isSubmitting ? 'Escalating...' : 'Escalate ticket'}
          </button>
        </form>
      ) : (
        <p className="field__hint">
          {ticket.priority === 'Critical'
            ? 'This ticket is already at Critical priority.'
            : 'Resolved and closed tickets cannot be escalated.'}
        </p>
      )}

      <div>
        <h3>Escalation history</h3>

        {isLoadingHistory && <p>Loading escalation history...</p>}

        {historyError && (
          <ErrorMessage
            message={historyError}
            onRetry={() => void loadHistory()}
          />
        )}

        {!isLoadingHistory && !historyError && history.length === 0 && (
          <p>No escalation records yet.</p>
        )}

        {!isLoadingHistory && history.length > 0 && (
          <ol>
            {history.map((item) => (
              <li key={item.id}>
                <p>
                  <strong>
                    {item.fromPriority} → {item.toPriority}
                  </strong>
                </p>

                <p>{item.reason}</p>

                <p>
                  <strong>Escalated by:</strong> {item.escalatedBy}
                </p>

                <p>
                  <strong>Assignment:</strong>{' '}
                  {formatAgent(item.fromAgentId)} →{' '}
                  {formatAgent(item.toAgentId)}
                </p>

                <p>
                  <strong>SLA deadline:</strong>{' '}
                  {item.fromDueAtUtc
                    ? formatDateTime(item.fromDueAtUtc)
                    : 'Not set'}
                  {' → '}
                  {item.toDueAtUtc
                    ? formatDateTime(item.toDueAtUtc)
                    : 'Not set'}
                </p>

                <p className="field__hint">
                  {formatDateTime(item.escalatedAtUtc)}
                </p>
              </li>
            ))}
          </ol>
        )}
      </div>
    </section>
  );
}