// Hand-written mirrors of the .NET API response/request bodies. The wire format is
// snake_case properties with UPPER_SNAKE_CASE enum values (Program.cs JSON options),
// and no null-omission is configured, so nullable value types arrive as an explicit
// null rather than a missing key — hence `| null` instead of optional members.
// Enums are string-literal unions because the toolchain's erasableSyntaxOnly rule
// forbids TS enums.

export type ContractStatus =
  | 'UPLOADED'
  | 'PARSING'
  | 'PARSED_SUCCESS'
  | 'PARSED_ERROR'
  | 'ARCHIVED'

export type RiskLevel = 'UNKNOWN' | 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL'

export interface PaginationMeta {
  current_page: number
  total_pages: number
  total_records: number
}

export interface PagedResponse<T> {
  data: T[]
  meta: PaginationMeta
}

// List item for GET /contracts; the single-contract resource adds uploaded_by.
export interface ContractSummary {
  id: string
  file_name: string
  file_uri: string
  status: ContractStatus
  overall_risk: RiskLevel
  created_at: string
  updated_at: string
}

export interface ContractDetail {
  id: string
  uploaded_by: string | null
  file_name: string
  file_uri: string
  status: ContractStatus
  overall_risk: RiskLevel
  created_at: string
  updated_at: string
}

export interface ClauseType {
  id: string
  name: string
  description: string | null
}

// The effective (newest) risk assessment nested under a clause; null until scored.
export interface ClauseRiskScore {
  id: string
  severity: RiskLevel
  rule_violated: string
  explanation: string
}

export interface Clause {
  id: string
  contract_id: string
  clause_type: ClauseType | null
  raw_text: string
  page_number: number | null
  byte_offset: number | null
  confidence_score: number | null
  risk_score: ClauseRiskScore | null
  created_at: string
}

// GET /contracts/{id}/clauses is not paginated: the whole clause set in a data envelope.
export interface ClauseListResponse {
  data: Clause[]
}

export interface ContractUploadResponse {
  id: string
  file_name: string
  status: ContractStatus
  created_at: string
  links: { status: string }
}

export interface RiskOverrideRequest {
  severity: RiskLevel
  explanation: string
}

export interface RiskOverrideResponse {
  id: string
  contract_clause_id: string
  severity: RiskLevel
  rule_violated: string
  explanation: string
  updated_at: string
}

export interface SearchClausesRequest {
  query: string
  similarity_threshold?: number
  limit?: number
}

// similarity_score is 1 - cosine_distance (higher is closer). clause_type is the
// type name string here (not the nested object the clause resource carries).
export interface SearchClauseResult {
  clause_id: string
  contract_id: string
  contract_file_name: string
  clause_type: string | null
  raw_text: string
  similarity_score: number
  page_number: number | null
}

export interface SearchMeta {
  execution_time_ms: number
  vector_distance_metric: string
}

export interface SearchClausesResponse {
  results: SearchClauseResult[]
  meta: SearchMeta
}

export interface DevTokenResponse {
  token: string
  token_type: string
  expires_at: string
  scope: string
  tenant_id: string
}
