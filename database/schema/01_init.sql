-- database/schema/01_init.sql
-- PostgreSQL 16 ContractAI Schema Initialization

BEGIN;

-- 1. Extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";
CREATE EXTENSION IF NOT EXISTS "vector";

-- 2. Enums
CREATE TYPE contract_status AS ENUM (
    'UPLOADED',
    'PARSING',
    'PARSED_SUCCESS',
    'PARSED_ERROR',
    'ARCHIVED'
);

CREATE TYPE risk_level AS ENUM (
    'UNKNOWN',
    'LOW',
    'MEDIUM',
    'HIGH',
    'CRITICAL'
);

-- 3. Core Tables

-- TENANTS
CREATE TABLE tenants (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL,
    is_deleted BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
COMMENT ON TABLE tenants IS 'Multi-tenant isolation root boundaries.';

-- USERS
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    email VARCHAR(255) NOT NULL UNIQUE,
    full_name VARCHAR(255) NOT NULL,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- CONTRACTS
CREATE TABLE contracts (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    uploaded_by UUID REFERENCES users(id) ON DELETE SET NULL,
    file_name VARCHAR(255) NOT NULL,
    file_uri VARCHAR(1024) NOT NULL,
    status contract_status DEFAULT 'UPLOADED',
    overall_risk risk_level DEFAULT 'UNKNOWN',
    is_deleted BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- CLAUSE_TYPES (Lookup Taxonomy)
CREATE TABLE clause_types (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(100) NOT NULL UNIQUE,
    description TEXT
);

-- CONTRACT_CLAUSES
CREATE TABLE contract_clauses (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    contract_id UUID NOT NULL REFERENCES contracts(id) ON DELETE CASCADE,
    clause_type_id UUID REFERENCES clause_types(id) ON DELETE SET NULL,
    raw_text TEXT NOT NULL,
    page_number INT,
    byte_offset INT,
    confidence_score FLOAT CHECK (confidence_score >= 0.0 AND confidence_score <= 1.0),
    embedding vector(1536),
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- CLAUSE_RISK_SCORES
CREATE TABLE clause_risk_scores (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    contract_clause_id UUID NOT NULL REFERENCES contract_clauses(id) ON DELETE CASCADE,
    severity risk_level NOT NULL,
    rule_violated VARCHAR(255) NOT NULL,
    explanation TEXT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- AUDIT_LOGS
CREATE TABLE audit_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    user_id UUID REFERENCES users(id) ON DELETE SET NULL,
    action VARCHAR(100) NOT NULL,
    old_data JSONB,
    new_data JSONB,
    timestamp TIMESTAMPTZ DEFAULT NOW()
);

-- 4. Triggers for updated_at
CREATE OR REPLACE FUNCTION update_modified_column() 
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER update_tenant_modtime BEFORE UPDATE ON tenants FOR EACH ROW EXECUTE FUNCTION update_modified_column();
CREATE TRIGGER update_user_modtime BEFORE UPDATE ON users FOR EACH ROW EXECUTE FUNCTION update_modified_column();
CREATE TRIGGER update_contract_modtime BEFORE UPDATE ON contracts FOR EACH ROW EXECUTE FUNCTION update_modified_column();
CREATE TRIGGER update_clause_modtime BEFORE UPDATE ON contract_clauses FOR EACH ROW EXECUTE FUNCTION update_modified_column();

-- 5. Indexing (B-Tree, GIN, and Vector HNSW)
CREATE INDEX idx_users_tenant_id ON users(tenant_id);
CREATE INDEX idx_contracts_tenant_id ON contracts(tenant_id);
CREATE INDEX idx_contracts_uploaded_by ON contracts(uploaded_by);
CREATE INDEX idx_contract_clauses_contract_id ON contract_clauses(contract_id);
CREATE INDEX idx_clause_risk_scores_clause_id ON clause_risk_scores(contract_clause_id);

-- Partial Index for Active processing
CREATE INDEX idx_contracts_unprocessed ON contracts(status) WHERE status IN ('UPLOADED', 'PARSING');

-- Trigram Index for fuzzy text search on clauses
CREATE INDEX idx_clauses_raw_text_trgm ON contract_clauses USING GIN (raw_text gin_trgm_ops);

-- HNSW Vector Index for Semantic Cosine Distance Search
CREATE INDEX idx_clauses_embedding_hnsw ON contract_clauses USING hnsw (embedding vector_cosine_ops) WITH (m = 16, ef_construction = 64);

-- 6. Row Level Security (RLS) Example Setup
ALTER TABLE contracts ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation_policy ON contracts 
    FOR ALL 
    USING (tenant_id = current_setting('app.current_tenant_id')::uuid);

COMMIT;
