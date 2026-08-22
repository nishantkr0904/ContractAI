import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from '@tanstack/react-query'
import type { ContractStatus } from '../../types/api'
import { fetchContract, fetchContracts, uploadContract } from './api'
import type { ContractListParams } from './api'

// Parsing runs in a background worker, so an upload lands in a non-terminal state and
// the client polls until it settles. ARCHIVED is terminal too — nothing further will
// move it — so polling stops there as well.
const TERMINAL_STATUSES = new Set<ContractStatus>([
  'PARSED_SUCCESS',
  'PARSED_ERROR',
  'ARCHIVED',
])
const POLL_INTERVAL_MS = 2000

export const contractKeys = {
  all: ['contracts'] as const,
  list: (params: ContractListParams) => ['contracts', params] as const,
  detail: (id: string) => ['contract', id] as const,
}

export function useContracts(params: ContractListParams) {
  return useQuery({
    queryKey: contractKeys.list(params),
    queryFn: () => fetchContracts(params),
    // Keep the previous page visible while the next one loads so paging doesn't flash
    // an empty table.
    placeholderData: keepPreviousData,
  })
}

export function useUploadContract() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (vars: { file: File; onProgress?: (percent: number) => void }) =>
      uploadContract(vars.file, vars.onProgress),
    onSuccess: () => {
      // The new row shows immediately (as UPLOADED); polling then reflects its progress.
      void queryClient.invalidateQueries({ queryKey: contractKeys.all })
    },
  })
}

// Polls one contract until parsing reaches a terminal state, then halts. Disabled when
// there is nothing to track. refetchInterval stays a pure scheduling function; the
// caller reacts to the terminal status (toast + list refresh).
export function useContractStatus(id: string | null) {
  return useQuery({
    queryKey: ['contract', id],
    queryFn: () => fetchContract(id as string),
    enabled: id !== null,
    refetchInterval: (query) => {
      const status = query.state.data?.status
      return status && TERMINAL_STATUSES.has(status) ? false : POLL_INTERVAL_MS
    },
  })
}

export function isTerminalStatus(status: ContractStatus): boolean {
  return TERMINAL_STATUSES.has(status)
}
