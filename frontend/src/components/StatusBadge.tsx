import type { ContractStatus } from '../types/api'

// Built-in Tailwind palette (always present) rather than the risk tokens: status is a
// pipeline state, not a risk level, so it shouldn't borrow the risk color language.
const statusStyle: Record<ContractStatus, { label: string; className: string }> = {
  UPLOADED: { label: 'Uploaded', className: 'bg-slate-100 text-slate-600' },
  PARSING: { label: 'Parsing', className: 'bg-amber-100 text-amber-700' },
  PARSED_SUCCESS: { label: 'Parsed', className: 'bg-green-100 text-green-700' },
  PARSED_ERROR: { label: 'Failed', className: 'bg-red-100 text-red-700' },
  ARCHIVED: { label: 'Archived', className: 'bg-slate-100 text-slate-500' },
}

export function StatusBadge({ status }: { status: ContractStatus }) {
  const { label, className } = statusStyle[status]
  return (
    <span className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${className}`}>
      {label}
    </span>
  )
}
