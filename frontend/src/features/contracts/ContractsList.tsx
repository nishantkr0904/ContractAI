import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import type { ContractStatus, RiskLevel } from '../../types/api'
import { RiskBadge } from '../../components/RiskBadge'
import { StatusBadge } from '../../components/StatusBadge'
import { formatDateTime } from '../../lib/format'
import { useContracts } from './hooks'

const PAGE_SIZE = 20

const STATUS_OPTIONS: ContractStatus[] = [
  'UPLOADED',
  'PARSING',
  'PARSED_SUCCESS',
  'PARSED_ERROR',
  'ARCHIVED',
]
const RISK_OPTIONS: RiskLevel[] = ['UNKNOWN', 'LOW', 'MEDIUM', 'HIGH', 'CRITICAL']
const SORT_OPTIONS = [
  { value: '-created_at', label: 'Newest first' },
  { value: 'created_at', label: 'Oldest first' },
  { value: 'file_name', label: 'Name (A–Z)' },
  { value: '-overall_risk', label: 'Risk (high–low)' },
]

export function ContractsList() {
  const navigate = useNavigate()
  const [page, setPage] = useState(1)
  const [sort, setSort] = useState('-created_at')
  const [status, setStatus] = useState('')
  const [risk, setRisk] = useState('')

  const query = useContracts({
    page,
    limit: PAGE_SIZE,
    sort,
    status: status || undefined,
    overall_risk: risk || undefined,
  })

  // Any filter/sort change resets to the first page: staying on page 5 of the old
  // result set would likely land past the end of the new one.
  function resetTo(setter: (value: string) => void, value: string) {
    setter(value)
    setPage(1)
  }

  const meta = query.data?.meta
  const rows = query.data?.data ?? []
  const totalPages = meta?.total_pages ?? 0

  return (
    <section>
      <div className="mb-3 flex flex-wrap items-center gap-2">
        <h2 className="mr-auto text-lg font-semibold text-slate-800">Contracts</h2>

        <select
          value={status}
          onChange={(event) => resetTo(setStatus, event.target.value)}
          className="rounded-lg border border-slate-200 bg-white px-2 py-1 text-sm text-slate-700"
        >
          <option value="">All statuses</option>
          {STATUS_OPTIONS.map((value) => (
            <option key={value} value={value}>
              {value}
            </option>
          ))}
        </select>

        <select
          value={risk}
          onChange={(event) => resetTo(setRisk, event.target.value)}
          className="rounded-lg border border-slate-200 bg-white px-2 py-1 text-sm text-slate-700"
        >
          <option value="">All risk levels</option>
          {RISK_OPTIONS.map((value) => (
            <option key={value} value={value}>
              {value}
            </option>
          ))}
        </select>

        <select
          value={sort}
          onChange={(event) => resetTo(setSort, event.target.value)}
          className="rounded-lg border border-slate-200 bg-white px-2 py-1 text-sm text-slate-700"
        >
          {SORT_OPTIONS.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      </div>

      <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-slate-200 bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
            <tr>
              <th className="px-4 py-2 font-medium">File</th>
              <th className="px-4 py-2 font-medium">Status</th>
              <th className="px-4 py-2 font-medium">Risk</th>
              <th className="px-4 py-2 font-medium">Uploaded</th>
            </tr>
          </thead>
          <tbody>
            {query.isPending ? (
              <tr>
                <td colSpan={4} className="px-4 py-8 text-center text-slate-400">
                  Loading…
                </td>
              </tr>
            ) : query.isError ? (
              <tr>
                <td colSpan={4} className="px-4 py-8 text-center text-risk-critical">
                  Could not load contracts.
                </td>
              </tr>
            ) : rows.length === 0 ? (
              <tr>
                <td colSpan={4} className="px-4 py-8 text-center text-slate-400">
                  No contracts yet. Upload a PDF to get started.
                </td>
              </tr>
            ) : (
              rows.map((contract) => (
                <tr
                  key={contract.id}
                  onClick={() => navigate(`/contracts/${contract.id}`)}
                  className="cursor-pointer border-b border-slate-100 last:border-0 hover:bg-slate-50"
                >
                  <td className="max-w-xs truncate px-4 py-3 font-medium text-slate-700">
                    {contract.file_name}
                  </td>
                  <td className="px-4 py-3">
                    <StatusBadge status={contract.status} />
                  </td>
                  <td className="px-4 py-3">
                    <RiskBadge risk={contract.overall_risk} />
                  </td>
                  <td className="px-4 py-3 text-slate-500">{formatDateTime(contract.created_at)}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <div className="mt-3 flex items-center justify-between text-sm text-slate-500">
        <span>{meta ? `${meta.total_records} total` : ''}</span>
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => setPage((current) => Math.max(1, current - 1))}
            disabled={page <= 1}
            className="rounded-lg border border-slate-200 px-3 py-1 disabled:opacity-40"
          >
            Previous
          </button>
          <span>
            Page {meta?.current_page ?? page}
            {totalPages ? ` of ${totalPages}` : ''}
          </span>
          <button
            type="button"
            onClick={() => setPage((current) => current + 1)}
            disabled={totalPages !== 0 && page >= totalPages}
            className="rounded-lg border border-slate-200 px-3 py-1 disabled:opacity-40"
          >
            Next
          </button>
        </div>
      </div>
    </section>
  )
}
