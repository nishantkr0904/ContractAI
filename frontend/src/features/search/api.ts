import { api } from '../../api/client'
import type { SearchClausesRequest, SearchClausesResponse } from '../../types/api'

export async function searchClauses(request: SearchClausesRequest) {
  const { data } = await api.post<SearchClausesResponse>('/search/clauses', request)
  return data
}
