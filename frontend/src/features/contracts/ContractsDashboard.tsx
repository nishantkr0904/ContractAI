import { FileUpload } from './FileUpload'
import { ContractsList } from './ContractsList'

export function ContractsDashboard() {
  return (
    <div className="space-y-6">
      <FileUpload />
      <ContractsList />
    </div>
  )
}
