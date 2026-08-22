import { useNavigate, useSearchParams } from 'react-router-dom'
import { useClauseSearch } from './hooks'

export function SearchResultsPage() {
  const [params] = useSearchParams()
  const query = params.get('q') ?? ''
  const navigate = useNavigate()
  const search = useClauseSearch(query)

  const results = search.data?.results ?? []
  const meta = search.data?.meta

  return (
    <div>
      <h1 className="text-xl font-semibold text-slate-800">Search results</h1>
      <p className="mt-1 text-sm text-slate-500">
        {query ? (
          <>
            for “{query}”
            {meta ? ` · ${results.length} matches · ${meta.execution_time_ms} ms` : ''}
          </>
        ) : (
          'Enter a query in the search bar above.'
        )}
      </p>

      <div className="mt-4">
        {search.isPending && query ? (
          <p className="text-sm text-slate-400">Searching…</p>
        ) : search.isError ? (
          <p className="text-sm text-risk-critical">Search failed. Please try again.</p>
        ) : query && results.length === 0 ? (
          <p className="text-sm text-slate-400">No clauses matched this query.</p>
        ) : (
          <ul className="space-y-2">
            {results.map((result) => (
              <li key={result.clause_id}>
                <button
                  type="button"
                  onClick={() =>
                    navigate(
                      result.page_number
                        ? `/contracts/${result.contract_id}?page=${result.page_number}`
                        : `/contracts/${result.contract_id}`,
                    )
                  }
                  className="block w-full rounded-lg border border-slate-200 bg-white px-4 py-3 text-left hover:border-slate-300 hover:bg-slate-50"
                >
                  <div className="flex items-center justify-between gap-2">
                    <span className="truncate text-sm font-medium text-slate-700">
                      {result.clause_type ?? 'Unclassified'}
                    </span>
                    <span className="shrink-0 rounded-full bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-600">
                      {Math.round(result.similarity_score * 100)}% match
                    </span>
                  </div>
                  <p className="mt-1 line-clamp-2 text-xs text-slate-500">{result.raw_text}</p>
                  <span className="mt-1 block text-xs text-slate-400">
                    {result.contract_file_name}
                    {result.page_number ? ` · Page ${result.page_number}` : ''}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}
