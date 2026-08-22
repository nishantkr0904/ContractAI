import type { Clause } from '../../types/api'
import { RiskBadge } from '../../components/RiskBadge'
import { riskBorderClass } from '../../lib/risk'
import { useClauses } from './hooks'
import { RiskOverrideControl } from './RiskOverrideControl'

interface ClauseSidebarProps {
  contractId: string
  selectedClauseId: string | null
  onSelectClause: (clause: Clause) => void
}

export function ClauseSidebar({
  contractId,
  selectedClauseId,
  onSelectClause,
}: ClauseSidebarProps) {
  const clauses = useClauses(contractId)

  return (
    <aside className="flex h-full flex-col">
      <h2 className="mb-3 text-lg font-semibold text-slate-800">
        Clauses
        {clauses.data ? <span className="ml-2 text-sm font-normal text-slate-400">{clauses.data.length}</span> : null}
      </h2>

      {clauses.isPending ? (
        <p className="text-sm text-slate-400">Loading clauses…</p>
      ) : clauses.isError ? (
        <p className="text-sm text-risk-critical">Could not load clauses.</p>
      ) : clauses.data.length === 0 ? (
        <p className="text-sm text-slate-400">No clauses detected for this contract.</p>
      ) : (
        <ul className="space-y-2 overflow-y-auto pr-1">
          {clauses.data.map((clause) => {
            const severity = clause.risk_score?.severity ?? 'UNKNOWN'
            const isSelected = clause.id === selectedClauseId
            return (
              <li
                key={clause.id}
                className={`rounded-lg border border-l-4 bg-white ${riskBorderClass[severity]} ${
                  isSelected ? 'border-slate-300 shadow-sm' : 'border-slate-200'
                }`}
              >
                <button
                  type="button"
                  onClick={() => onSelectClause(clause)}
                  className="block w-full px-3 py-2 text-left"
                >
                  <div className="flex items-center justify-between gap-2">
                    <span className="truncate text-sm font-medium text-slate-700">
                      {clause.clause_type?.name ?? 'Unclassified'}
                    </span>
                    <RiskBadge risk={severity} />
                  </div>
                  <p className="mt-1 line-clamp-3 text-xs text-slate-500">{clause.raw_text}</p>
                  <span className="mt-1 block text-xs text-slate-400">
                    {clause.page_number ? `Page ${clause.page_number}` : 'Page —'}
                  </span>
                </button>

                {isSelected && (
                  <div className="px-3 pb-3">
                    {clause.risk_score?.explanation && (
                      <p className="mb-2 rounded bg-slate-50 px-2 py-1 text-xs text-slate-500">
                        {clause.risk_score.explanation}
                      </p>
                    )}
                    <RiskOverrideControl
                      contractId={contractId}
                      clauseId={clause.id}
                      currentSeverity={severity}
                    />
                  </div>
                )}
              </li>
            )
          })}
        </ul>
      )}
    </aside>
  )
}
