import { api } from '../../api/client'
import type {
  ContractDetail,
  ContractSummary,
  ContractUploadResponse,
  PagedResponse,
} from '../../types/api'

// Only fields the UI actually varies are modeled; page/limit always sent, the rest
// omitted when unset so the server applies its defaults.
export interface ContractListParams {
  page: number
  limit: number
  sort?: string
  status?: string
  overall_risk?: string
}

export async function fetchContracts(
  params: ContractListParams,
): Promise<PagedResponse<ContractSummary>> {
  const { data } = await api.get<PagedResponse<ContractSummary>>('/contracts', { params })
  return data
}

export async function fetchContract(id: string): Promise<ContractDetail> {
  const { data } = await api.get<ContractDetail>(`/contracts/${id}`)
  return data
}

// The field name must be "file" to bind to the controller's IFormFile parameter.
// Content-Type is intentionally left unset so axios generates the multipart boundary.
export async function uploadContract(
  file: File,
  onProgress?: (percent: number) => void,
): Promise<ContractUploadResponse> {
  const form = new FormData()
  form.append('file', file)

  const { data } = await api.post<ContractUploadResponse>('/contracts/upload', form, {
    onUploadProgress: (event) => {
      if (onProgress && event.total) {
        onProgress(Math.round((event.loaded / event.total) * 100))
      }
    },
  })
  return data
}
