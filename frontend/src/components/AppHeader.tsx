import { Link } from 'react-router-dom'
import { useAuth } from '../auth/authContext'
import { SearchBar } from '../features/search/SearchBar'

export function AppHeader() {
  const { scope, logout } = useAuth()

  return (
    <header className="border-b border-slate-200 bg-white">
      <div className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-6 py-3">
        <Link to="/" className="shrink-0 text-lg font-semibold text-slate-800">
          ContractAI
        </Link>
        <SearchBar />
        <div className="flex shrink-0 items-center gap-3 text-sm text-slate-500">
          {scope && <span className="rounded bg-slate-100 px-2 py-0.5">{scope}</span>}
          <button
            type="button"
            onClick={logout}
            className="rounded-lg border border-slate-200 px-3 py-1 text-slate-700 hover:border-slate-300"
          >
            Sign out
          </button>
        </div>
      </div>
    </header>
  )
}
