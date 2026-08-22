import { useQuery } from '@tanstack/react-query'
import { searchClauses } from './api'

// Driven by the query string in the URL: the search bar navigates to /search?q=…, this
// runs only when q is non-empty, and staleTime keeps an identical repeat search from
// re-embedding on the server (each query costs an embedding round-trip).
export function useClauseSearch(query: string) {
  const trimmed = query.trim()
  return useQuery({
    queryKey: ['search', trimmed],
    queryFn: () => searchClauses({ query: trimmed }),
    enabled: trimmed.length > 0,
    staleTime: 60_000,
  })
}
