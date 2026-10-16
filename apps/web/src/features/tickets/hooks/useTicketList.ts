
import { useEffect, useRef, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type {
  PagedResult,
  TicketFilters,
  TicketListItem,
} from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  slaStatus: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

interface State {
  isLoading: boolean;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

/** Owns the ticket list's filters and the data they produce. */
export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  const [state, setState] = useState<State>({ isLoading: true });

  const previousFilters = useRef(filters);

  // A stable dependency that changes whenever a serialized filter value changes.
  const filterKey = JSON.stringify(filters);

  const load = async () => {
    setState((current) => ({
      ...current,
      isLoading: true,
      error: undefined,
    }));

    try {
      const data = await ticketsApi.getTickets(filters);

      setState({
        isLoading: false,
        data,
        error: undefined,
      });
    } catch (error) {
      const message =
        error instanceof ApiError
          ? error.message
          : 'Unable to load tickets.';

      setState((current) => ({
        ...current,
        isLoading: false,
        error: message,
      }));
    }
  };

  useEffect(() => {
    const previous = previousFilters.current;
    const searchChanged = previous.search !== filters.search;

    // Detect changes to any filter, including future filters.
    // Ignore search and page when deciding whether to debounce.
    const otherFilterChanged = Object.keys(filters).some((key) => {
      if (key === 'search' || key === 'page') {
        return false;
      }

      const filterName = key as keyof TicketFilters;

      return previous[filterName] !== filters[filterName];
    });

    previousFilters.current = filters;

    // Debounce search-only changes; fetch immediately for other changes.
    const delay =
      searchChanged && !otherFilterChanged ? DEBOUNCE_MS : 0;

    const handle = window.setTimeout(() => {
      void load();
    }, delay);

    return () => window.clearTimeout(handle);

    // filterKey tracks all filter values; load uses the matching render's filters.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filterKey]);

  const updateFilters = (patch: Partial<TicketFilters>) =>
    setFilters((current) => ({
      ...current,
      ...patch,
      page: patch.page ?? 1,
    }));

  const resetFilters = () => setFilters(emptyFilters);

  return {
    filters,
    updateFilters,
    resetFilters,
    reload: load,
    ...state,
  };
}
