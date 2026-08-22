import { useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'

// Submitting routes to /search?q=… rather than searching in place, so a search is
// linkable/refreshable and the results page owns the request. Seeded from the current
// ?q so the field reflects the active search when landing on the results page.
export function SearchBar() {
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const [value, setValue] = useState(params.get('q') ?? '')

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const query = value.trim()
    if (!query) return
    navigate(`/search?q=${encodeURIComponent(query)}`)
  }

  return (
    <form onSubmit={handleSubmit} className="mx-4 max-w-md flex-1">
      <input
        type="search"
        value={value}
        onChange={(event) => setValue(event.target.value)}
        placeholder="Search clauses…"
        aria-label="Search clauses"
        className="w-full rounded-lg border border-slate-200 bg-slate-50 px-3 py-1.5 text-sm text-slate-700 focus:border-slate-400 focus:bg-white focus:outline-none"
      />
    </form>
  )
}
