CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- 1.1 Contracts table (đơn giản hoá theo form tạo)
CREATE TABLE IF NOT EXISTS public.contracts (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    scope_type          TEXT NOT NULL CHECK (scope_type IN ('project','package')),
    project_id          UUID NOT NULL,
    package_id          UUID NULL,

    code                TEXT NOT NULL,
    name                TEXT NOT NULL,
    value_vnd           NUMERIC(18,2) NOT NULL CHECK (value_vnd > 0),
    warranty_value_vnd  NUMERIC(18,2) NOT NULL DEFAULT 0 CHECK (warranty_value_vnd >= 0),

    start_date          DATE NOT NULL,
    end_date            DATE NOT NULL,
    description         TEXT NULL,

    payment_schedule    JSONB NOT NULL DEFAULT '[]'::jsonb,

    state               TEXT NOT NULL DEFAULT 'draft',
    signature_status    TEXT NOT NULL DEFAULT 'none',

    created_by          UUID NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by          UUID NULL,
    updated_at          TIMESTAMPTZ NULL,
    version             INT NOT NULL DEFAULT 1
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_contracts_project_code
ON public.contracts(project_id, code);

CREATE INDEX IF NOT EXISTS ix_contracts_project ON public.contracts(project_id);
CREATE INDEX IF NOT EXISTS ix_contracts_package ON public.contracts(package_id);

CREATE TABLE IF NOT EXISTS public.outbox_events (
    id BIGSERIAL PRIMARY KEY,
    aggregate_type TEXT NOT NULL,
    aggregate_id   UUID NOT NULL,
    event_type     TEXT NOT NULL,
    payload        JSONB NOT NULL,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    processed_at   TIMESTAMPTZ NULL
);
CREATE INDEX IF NOT EXISTS ix_outbox_events_unprocessed
ON public.outbox_events(aggregate_type, processed_at)
WHERE processed_at IS NULL;

CREATE TABLE IF NOT EXISTS public.projects (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code TEXT NOT NULL,
    name TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS public.bid_packages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    project_id UUID NOT NULL,
    code TEXT NOT NULL,
    name TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_bid_packages_project ON public.bid_packages(project_id);
