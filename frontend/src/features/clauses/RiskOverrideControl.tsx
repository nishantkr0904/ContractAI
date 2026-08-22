import { useState } from 'react'
import type { FormEvent } from 'react'
import type { RiskLevel } from '../../types/api'
import { OVERRIDE_LEVELS, riskLabel } from '../../lib/risk'
import { useToast } from '../../components/toast/toastContext'
import { useOverrideRisk } from './hooks'

type OverrideLevel = Exclude<RiskLevel, 'UNKNOWN'>

interface RiskOverrideControlProps {
  contractId: string
  clauseId: string
  currentSeverity: RiskLevel
}

// The API requires a non-empty explanation for an override (it becomes the audit
// record), so this is a severity picker plus a justification field rather than a bare
// dropdown; Save stays disabled until both are valid.
export function RiskOverrideControl({
  contractId,
  clauseId,
  currentSeverity,
}: RiskOverrideControlProps) {
  const { showToast } = useToast()
  const override = useOverrideRisk(contractId)
  const [severity, setSeverity] = useState<OverrideLevel>(
    currentSeverity === 'UNKNOWN' ? 'LOW' : currentSeverity,
  )
  const [explanation, setExplanation] = useState('')

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const trimmed = explanation.trim()
    if (!trimmed) return

    override.mutate(
      { clauseId, body: { severity, explanation: trimmed } },
      {
        onSuccess: () => {
          showToast('Risk severity updated.', 'success')
          setExplanation('')
        },
        onError: () => showToast('Could not update risk severity.', 'error'),
      },
    )
  }

  return (
    <form onSubmit={handleSubmit} className="mt-3 border-t border-slate-100 pt-3">
      <label className="block text-xs font-medium text-slate-500">Override risk</label>
      <div className="mt-1 flex gap-2">
        <select
          value={severity}
          onChange={(event) => setSeverity(event.target.value as OverrideLevel)}
          className="rounded-lg border border-slate-200 bg-white px-2 py-1 text-sm text-slate-700"
        >
          {OVERRIDE_LEVELS.map((level) => (
            <option key={level} value={level}>
              {riskLabel[level]}
            </option>
          ))}
        </select>
      </div>
      <textarea
        value={explanation}
        onChange={(event) => setExplanation(event.target.value)}
        placeholder="Why is this the correct severity?"
        rows={2}
        maxLength={4000}
        className="mt-2 w-full resize-y rounded-lg border border-slate-200 px-2 py-1 text-sm text-slate-700"
      />
      <button
        type="submit"
        disabled={override.isPending || explanation.trim().length === 0}
        className="mt-2 rounded-lg bg-slate-800 px-3 py-1 text-sm font-medium text-white hover:bg-slate-700 disabled:opacity-50"
      >
        {override.isPending ? 'Saving…' : 'Save override'}
      </button>
    </form>
  )
}
