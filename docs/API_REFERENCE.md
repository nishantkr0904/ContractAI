# ContractAI API Reference

## 1. API Overview & Standards

### Base URL & Versioning
All API requests are made over HTTPS. The current version of the API is `v1`. 
**Base URL:** `https://api.contractai.internal/api/v1`

### Authentication
The API utilizes OAuth 2.0 with JWT (JSON Web Token) Bearer authentication.
- **Header:** `Authorization: Bearer <your_jwt_token>`
- **Scopes:**
  - `app_reader`: Allows read-only access (GET requests).
  - `app_writer`: Allows read/write access (POST, PATCH, PUT, DELETE requests).

### Multi-Tenancy
ContractAI is a strictly isolated multi-tenant application. 
**Important:** You do **NOT** pass a `tenant_id` in the URL or request body. The `.NET 9` backend automatically resolves the tenant context securely from the `tenant_id` claim embedded within your authenticated JWT. Any attempt to access resources belonging to a different tenant will result in a `404 Not Found` (to avoid data enumeration) or `403 Forbidden`.

### Standard Responses & Error Handling
All API errors follow the **RFC 7807 Problem Details** standard. 

- `400 Bad Request`: Validation errors or malformed payloads.
- `401 Unauthorized`: Missing or invalid JWT.
- `403 Forbidden`: Insufficient scope/role.
- `500 Internal Server Error`: Unhandled server exceptions.

**Sample 400 Bad Request Payload:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "traceId": "00-b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8-c9c9c9c9c9c9c9c9-00",
  "errors": {
    "similarity_threshold": [
      "The similarity threshold must be between 0.0 and 1.0."
    ]
  }
}
```

### Pagination & Filtering
Collections are paginated using standard query parameters:
- `page`: The page number to retrieve (default: `1`).
- `limit`: Number of records per page (default: `20`, max: `100`).
- `sort`: Field to sort by. Prefix with `-` for descending order (e.g., `-created_at`).

Responses for collections are wrapped in a standard pagination envelope.

---

## 2. Endpoint Specifications

### A. Contracts Management

#### Upload Contract
Uploads a PDF contract for asynchronous clause extraction and risk analysis.

- **HTTP Method & Route:** `POST /api/v1/contracts/upload`
- **Description:** Accepts a multipart/form-data PDF file. The file is streamed to blob storage, a database record is created in `UPLOADED` state, and a background task is triggered in the C++ parsing engine.
- **Authorization:** `app_writer`

**Request format:** `multipart/form-data`
- `file`: (Binary PDF File, max 50MB)

**Response:** `202 Accepted`
```json
{
  "id": "e4b9c1d2-a7f3-4b8c-9d1e-5f6a7b8c9d0e",
  "file_name": "Vendor_Agreement_2026.pdf",
  "status": "UPLOADED",
  "created_at": "2026-08-20T10:00:00Z",
  "links": {
    "status": "/api/v1/contracts/e4b9c1d2-a7f3-4b8c-9d1e-5f6a7b8c9d0e"
  }
}
```

#### List Contracts
Retrieves a paginated list of contracts within the tenant.

- **HTTP Method & Route:** `GET /api/v1/contracts`
- **Description:** Supports filtering by `status` and `overall_risk`.
- **Authorization:** `app_reader`

**Query Parameters:**
- `status` (optional): Filter by `UPLOADED`, `PARSING`, `PARSED_SUCCESS`, `PARSED_ERROR`, `ARCHIVED`.
- `overall_risk` (optional): Filter by `LOW`, `MEDIUM`, `HIGH`, `CRITICAL`.
- Standard pagination params (`page`, `limit`, `sort`).

**Response:** `200 OK`
```json
{
  "data": [
    {
      "id": "e4b9c1d2-a7f3-4b8c-9d1e-5f6a7b8c9d0e",
      "file_name": "Vendor_Agreement_2026.pdf",
      "file_uri": "s3://contracts/tenant_xyz/Vendor_Agreement_2026.pdf",
      "status": "PARSED_SUCCESS",
      "overall_risk": "MEDIUM",
      "created_at": "2026-08-20T10:00:00Z",
      "updated_at": "2026-08-20T10:01:15Z"
    }
  ],
  "meta": {
    "current_page": 1,
    "total_pages": 5,
    "total_records": 92
  }
}
```

#### Get Contract Metadata
Fetches a single contract's metadata by ID.

- **HTTP Method & Route:** `GET /api/v1/contracts/{id}`
- **Description:** Retrieves metadata for a specific contract. Returns 404 if not found or belongs to another tenant.
- **Authorization:** `app_reader`

**Response:** `200 OK`
```json
{
  "id": "e4b9c1d2-a7f3-4b8c-9d1e-5f6a7b8c9d0e",
  "uploaded_by": "f8a1c9e4-3b2d-4f1a-8c7e-6d5b4a3c2d1e",
  "file_name": "Vendor_Agreement_2026.pdf",
  "file_uri": "s3://contracts/tenant_xyz/Vendor_Agreement_2026.pdf",
  "status": "PARSED_SUCCESS",
  "overall_risk": "MEDIUM",
  "created_at": "2026-08-20T10:00:00Z",
  "updated_at": "2026-08-20T10:01:15Z"
}
```

---

### B. Clauses & Risk Intelligence

#### Get Contract Clauses
Retrieves all extracted clauses and their associated AI risk scores for a specific document.

- **HTTP Method & Route:** `GET /api/v1/contracts/{id}/clauses`
- **Description:** Returns the text boundaries, parser confidence, and nested risk evaluations.
- **Authorization:** `app_reader`

**Response:** `200 OK`
```json
{
  "data": [
    {
      "id": "c1a2b3c4-d5e6-4f7a-8b9c-0d1e2f3a4b5c",
      "contract_id": "e4b9c1d2-a7f3-4b8c-9d1e-5f6a7b8c9d0e",
      "clause_type": {
        "id": "a1b2c3d4-e5f6-4a5b-8c7d-9e0f1a2b3c4d",
        "name": "Indemnification",
        "description": "Clauses dictating liability and indemnification terms."
      },
      "raw_text": "The Vendor shall indemnify, defend, and hold harmless the Client against any and all claims arising out of intellectual property infringement.",
      "page_number": 12,
      "byte_offset": 45092,
      "confidence_score": 0.98,
      "risk_score": {
        "id": "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e",
        "severity": "HIGH",
        "rule_violated": "Uncapped IP Indemnity",
        "explanation": "The clause does not specify a monetary cap for intellectual property infringement indemnification, posing significant financial risk."
      },
      "created_at": "2026-08-20T10:01:12Z"
    }
  ]
}
```

#### Override Clause Risk
Allows human reviewers to override the AI-generated risk assessment.

- **HTTP Method & Route:** `PATCH /api/v1/clauses/{id}/risk`
- **Description:** Updates the risk severity and records a human justification. This action is audited.
- **Authorization:** `app_writer`

**Request Body:** `application/json`
```json
{
  "severity": "LOW",
  "explanation": "Reviewed by General Counsel. Existing master services agreement caps this liability."
}
```

**Response:** `200 OK`
```json
{
  "id": "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e",
  "contract_clause_id": "c1a2b3c4-d5e6-4f7a-8b9c-0d1e2f3a4b5c",
  "severity": "LOW",
  "rule_violated": "Uncapped IP Indemnity (Human Override)",
  "explanation": "Reviewed by General Counsel. Existing master services agreement caps this liability.",
  "updated_at": "2026-08-20T14:30:00Z"
}
```

---

### C. Semantic & AI Search

#### Semantic Clause Search
Performs a highly optimized vector search across the tenant's parsed clauses based on natural language intent.

- **HTTP Method & Route:** `POST /api/v1/search/clauses`
- **Description:** The .NET backend takes the `query` string, calls an embedding model (e.g., OpenAI) to convert it to a 1536-D vector, and executes an HNSW Cosine Distance search (`pgvector`) against the database.
- **Authorization:** `app_reader`

**Request Body:** `application/json`
```json
{
  "query": "net 30 payment terms and late fees",
  "similarity_threshold": 0.75,
  "limit": 5
}
```

**Response:** `200 OK`
```json
{
  "results": [
    {
      "clause_id": "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a",
      "contract_id": "e4b9c1d2-a7f3-4b8c-9d1e-5f6a7b8c9d0e",
      "contract_file_name": "Vendor_Agreement_2026.pdf",
      "clause_type": "Payment Terms",
      "raw_text": "Invoices shall be payable within thirty (30) days of receipt. Overdue balances are subject to a 1.5% monthly late fee.",
      "similarity_score": 0.92,
      "page_number": 4
    },
    {
      "clause_id": "a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d",
      "contract_id": "f5a6b7c8-d9e0-4f1a-2b3c-4d5e6f7a8b9c",
      "contract_file_name": "MSA_AcmeCorp.pdf",
      "clause_type": "Payment Terms",
      "raw_text": "Payment is due net 30 days from the invoice date.",
      "similarity_score": 0.88,
      "page_number": 2
    }
  ],
  "meta": {
    "execution_time_ms": 112,
    "vector_distance_metric": "cosine"
  }
}
```
