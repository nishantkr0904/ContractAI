import { useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import type { Clause } from '../../types/api'
import { RiskBadge } from '../../components/RiskBadge'
import { StatusBadge } from '../../components/StatusBadge'
import { ClauseSidebar } from '../clauses/ClauseSidebar'
import { DocumentViewer } from './DocumentViewer'
import { fetchContract } from './api'

export function ContractDetailPage() {
  const { id } = useParams<{ id: string }>()
  const contractId = id ?? ''
  const [searchParams] = useSearchParams()

  // Search results deep-link to a clause's page via ?page=N; anything else opens on
  // page 1. Parsed for the initial paint and re-applied below so a second search hit
  // into the same contract still re-jumps the viewer.
  const pageParam = Number(searchParams.get('page'))
  const targetPage = Number.isInteger(pageParam) && pageParam > 0 ? pageParam : 1

  const [activePage, setActivePage] = useState(targetPage)
  const [syncedPage, setSyncedPage] = useState(targetPage)
  const [selectedClauseId, setSelectedClauseId] = useState<string | null>(null)

  // When a search result deep-links in with a new ?page=N, jump the viewer there.
  // Adjusting state during render (React's recommended alternative to an effect) so the
  // jump lands before paint. Local Prev/Next and sidebar clicks change activePage
  // without touching the URL, so this only re-fires on a real navigation.
  if (targetPage !== syncedPage) {
    setSyncedPage(targetPage)
    setActivePage(targetPage)
  }

  // Shares the ['contract', id] key with the upload poller, so a contract that
  // finished parsing elsewhere is already warm in the cache here.
  const contract = useQuery({
    queryKey: ['contract', contractId],
    queryFn: () => fetchContract(contractId),
    enabled: contractId !== '',
  })

  // Selecting a clause both highlights it and jumps the viewer to its page.
  function handleSelectClause(clause: Clause) {
    setSelectedClauseId(clause.id)
    if (clause.page_number) {
      setActivePage(clause.page_number)
    }
  }

  return (
    <div>
      <div className="mb-4">
        <Link to="/" className="text-sm text-slate-500 hover:text-slate-700">
          ← Back to contracts
        </Link>
        <div className="mt-1 flex flex-wrap items-center gap-3">
          <h1 className="truncate text-xl font-semibold text-slate-800">
            {contract.data?.file_name ?? 'Contract'}
          </h1>
          {contract.data && <StatusBadge status={contract.data.status} />}
          {contract.data && <RiskBadge risk={contract.data.overall_risk} />}
        </div>
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-[1fr_360px]">
        <DocumentViewer contractId={contractId} page={activePage} onPageChange={setActivePage} />
        <ClauseSidebar
          contractId={contractId}
          selectedClauseId={selectedClauseId}
          onSelectClause={handleSelectClause}
        />
      </div>
    </div>
  )
}
