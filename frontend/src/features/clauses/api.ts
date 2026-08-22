import { api } from '../../api/client'
import type {
  ClauseListResponse,
  RiskOverrideRequest,
  RiskOverrideResponse,
} from '../../types/api'

export async function fetchClauses(contractId: string) {
  const { data } = await api.get<ClauseListResponse>(`/contracts/${contractId}/clauses`)
  return data.data
}

export async function overrideClauseRisk(clauseId: string, body: RiskOverrideRequest) {
  const { data } = await api.patch<RiskOverrideResponse>(`/clauses/${clauseId}/risk`, body)
  return data
}
