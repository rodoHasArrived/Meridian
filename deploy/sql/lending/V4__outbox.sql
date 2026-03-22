-- =============================================================================
-- Transactional outbox
-- =============================================================================
-- Flyway naming: V4__outbox.sql
--
-- Design:
--   lending.outbox — reliable event delivery; written in the same transaction
--                    as the domain event append to guarantee at-least-once delivery.
--
-- Processing model:
--   The outbox dispatcher polls for rows where processed_at IS NULL, ordered by
--   created_at ASC. After successful downstream dispatch it sets processed_at to NOW().
--   On failure it increments retry_count and sets last_error.  Rows with
--   retry_count > max_retries are moved to the dead-letter state (dead_lettered_at).
--
-- Partitioning note:
--   When the table grows beyond ~50 M rows, range-partition by created_at month
--   and archive older partitions to cold storage (pg_partman is the recommended tool).
-- =============================================================================

CREATE TABLE IF NOT EXISTS lending.outbox (
    id               BIGSERIAL        PRIMARY KEY,
    -- The aggregate that produced this event.
    aggregate_id     UUID             NOT NULL,
    -- "LoanContract" | "LoanServicing"
    aggregate_type   TEXT             NOT NULL,
    -- Event type name (e.g. "DrawdownExecuted", "PaymentConfirmed").
    event_type       TEXT             NOT NULL,
    -- JSON payload — full event body for downstream consumers.
    payload_json     TEXT             NOT NULL,
    -- Optional correlation / causation identifiers for distributed tracing.
    correlation_id   UUID,
    causation_id     UUID,
    created_at       TIMESTAMPTZ      NOT NULL DEFAULT NOW(),
    -- Set to NOW() after successful dispatch.
    processed_at     TIMESTAMPTZ,
    -- Incremented on each failed dispatch attempt.
    retry_count      SMALLINT         NOT NULL DEFAULT 0,
    -- Set to NOW() when retry_count exceeds the configured maximum.
    dead_lettered_at TIMESTAMPTZ,
    -- Last error message, for diagnostics.
    last_error       TEXT
);

-- Fast query for the dispatcher: unprocessed, non-dead-lettered entries in creation order.
CREATE INDEX IF NOT EXISTS ix_outbox_pending
    ON lending.outbox (created_at ASC)
    WHERE processed_at IS NULL AND dead_lettered_at IS NULL;

-- Lookup by aggregate_id (used for replay / reprocessing a specific aggregate).
CREATE INDEX IF NOT EXISTS ix_outbox_aggregate
    ON lending.outbox (aggregate_id, created_at ASC);
