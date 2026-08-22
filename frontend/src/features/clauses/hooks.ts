import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { RiskOverrideRequest } from '../../types/api'
import { fetchClauses, overrideClauseRisk } from './api'

export const clauseKeys = {
  list: (contractId: string) => ['clauses', contractId] as const,
}

export function useClauses(contractId: string) {
  return useQuery({
    queryKey: clauseKeys.list(contractId),
    queryFn: () => fetchClauses(contractId),
  })
}

// On success the whole clause set is refetched rather than patched in place: an
// override carries forward the prior rule label ("… (Human Override)") and a new
// updated timestamp the client would otherwise have to reconstruct, so the server's
// copy is the source of truth.
export function useOverrideRisk(contractId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (vars: { clauseId: string; body: RiskOverrideRequest }) =>
      overrideClauseRisk(vars.clauseId, vars.body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: clauseKeys.list(contractId) })
    },
  })
}
