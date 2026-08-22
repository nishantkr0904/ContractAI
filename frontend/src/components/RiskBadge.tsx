import type { RiskLevel } from '../types/api'
import { riskDotClass, riskLabel } from '../lib/risk'

// A neutral pill with a colored dot: the pill stays legible in a dense table while the
// dot carries the severity color from the shared risk tokens.
export function RiskBadge({ risk }: { risk: RiskLevel }) {
  return (
    <span className="inline-flex items-center gap-1.5 rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-medium text-slate-700">
      <span className={`h-2 w-2 rounded-full ${riskDotClass[risk]}`} />
      {riskLabel[risk]}
    </span>
  )
}
