-- =============================================================================
-- Loan Servicing aggregate event store
-- =============================================================================
-- Sharpino-pattern tables for the Loan Servicing aggregate.
-- Flyway naming: V3__loan_servicing_events.sql
--
-- Design:
--   loan_servicing_events   — append-only event log; one row per ServicingEvent
--   loan_servicing_snapshots — periodic state snapshots for fast replay
--
-- The Loan Contract aggregate (V1) and the Loan Servicing aggregate (this file)
-- are two separate write-side streams that share the same aggregate_id (= loan_id).
-- They are stored in separate tables to keep their event streams independent.
-- =============================================================================

-- ---------------------------------------------------------------------------
-- Event log
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS lending.loan_servicing_events (
    id               BIGSERIAL        PRIMARY KEY,
    aggregate_id     UUID             NOT NULL,
    sequence_number  BIGINT           NOT NULL,
    event_json       TEXT             NOT NULL,
    created_at       TIMESTAMPTZ      NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_loan_servicing_events_stream
        UNIQUE (aggregate_id, sequence_number)
);

CREATE INDEX IF NOT EXISTS ix_loan_servicing_events_aggregate
    ON lending.loan_servicing_events (aggregate_id, sequence_number ASC);

-- ---------------------------------------------------------------------------
-- Snapshot table
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS lending.loan_servicing_snapshots (
    id               BIGSERIAL        PRIMARY KEY,
    aggregate_id     UUID             NOT NULL,
    sequence_number  BIGINT           NOT NULL,
    state_json       TEXT             NOT NULL,
    created_at       TIMESTAMPTZ      NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_loan_servicing_snapshots_version
        UNIQUE (aggregate_id, sequence_number)
);

CREATE INDEX IF NOT EXISTS ix_loan_servicing_snapshots_aggregate
    ON lending.loan_servicing_snapshots (aggregate_id, sequence_number DESC);
