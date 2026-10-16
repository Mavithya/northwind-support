import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { page, ticketListItem } from '../../../test/fixtures';
import { useTicketList } from './useTicketList';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    getTickets: vi.fn(),
  },
}));

const getTickets = vi.mocked(ticketsApi.getTickets);

describe('useTicketList', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getTickets.mockResolvedValue(page([ticketListItem()]));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it('searches with the text the user typed, once the typing settles', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.updateFilters({ search: 'invoice' });
    });

    // Still inside the debounce window: no second request yet.
    expect(getTickets).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: 'invoice', page: 1 }),
    );

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data?.items).toHaveLength(1);
  });


  
it('fetches tickets when the status filter changes', async () => {
  const { result } = renderHook(() => useTicketList());

  // Wait for the initial request.
  await act(async () => {
    await vi.advanceTimersByTimeAsync(300);
  });

  expect(getTickets).toHaveBeenCalledTimes(1);

  // Change the status filter.
  act(() => {
    result.current.updateFilters({ status: 'InProgress' });
  });

  // Allow the scheduled request to run.
  await act(async () => {
    await vi.advanceTimersByTimeAsync(0);
  });

  // A second request should have been sent with the new status.
  expect(getTickets).toHaveBeenCalledTimes(2);

  expect(getTickets).toHaveBeenLastCalledWith(
    expect.objectContaining({
      status: 'InProgress',
      page: 1,
    }),
  );
});

it('fetches tickets immediately when the SLA filter changes', async () => {
  const { result } = renderHook(() => useTicketList());

  // Complete the initial request.
  await act(async () => {
    await vi.advanceTimersByTimeAsync(300);
  });

  expect(getTickets).toHaveBeenCalledTimes(1);

  // Change the SLA filter.
  act(() => {
    result.current.updateFilters({ slaStatus: 'AtRisk' });
  });

  // SLA filter changes must not wait for the search debounce.
  await act(async () => {
    await vi.advanceTimersByTimeAsync(0);
  });

  expect(getTickets).toHaveBeenCalledTimes(2);
  expect(getTickets).toHaveBeenLastCalledWith(
    expect.objectContaining({
      slaStatus: 'AtRisk',
      page: 1,
    }),
  );
});

});
