-- =============================================================================
-- TimescaleDB analytics layer
-- =============================================================================
-- Flyway naming: V6__timescale_analytics.sql
--
-- Requires: TimescaleDB extension (CREATE EXTENSION timescaledb;)
-- Target schema: lending_analytics (separate from the operational lending schema)
--
-- Design:
--   lending_analytics.benchmark_fixings  — reference rate fixings (SOFR, EURIBOR, etc.)
--   lending_analytics.loan_daily_snapshots — daily per-loan economic snapshot
--   lending_analytics.portfolio_metrics  — daily portfolio-level aggregates
--
-- All three tables are TimescaleDB hypertables partitioned by their time column.
-- Retention policies and continuous aggregates should be configured after creation.
-- =============================================================================

-- Requires TimescaleDB
CREATE EXTENSION IF NOT EXISTS timescaledb CASCADE;

CREATE SCHEMA IF NOT EXISTS lending_analytics;

-- ---------------------------------------------------------------------------
-- Benchmark fixings: reference rate fixings per day per index
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS lending_analytics.benchmark_fixings (
    -- Partition dimension (time column for hypertable)
    fixing_date      DATE          NOT NULL,
    -- Standard index name: 'SOFR', 'EURIBOR3M', 'SONIA', 'LIBOR3M', etc.
    index_name       TEXT          NOT NULL,
    -- Fixing rate as a decimal (e.g. 0.0532 = 5.32 %)
    rate             NUMERIC(12,8) NOT NULL,
    -- Source of the fixing: 'CME', 'ECB', 'BOE', 'MANUAL', etc.
    source           TEXT          NOT NULL DEFAULT 'MANUAL',
    created_at       TIMESTAMPTZ   NOT NULL DEFAULT NOW(),

    PRIMARY KEY (fixing_date, index_name)
);

-- Create hypertable partitioned by fixing_date, 1-month chunks
SELECT create_hypertable(
    'lending_analytics.benchmark_fixings',
    'fixing_date',
    chunk_time_interval => INTERVAL '1 month',
    if_not_exists => TRUE
);

CREATE INDEX IF NOT EXISTS ix_benchmark_fixings_index
    ON lending_analytics.benchmark_fixings (index_name, fixing_date DESC);

-- ---------------------------------------------------------------------------
-- Loan daily snapshots: per-loan economic state snapshot at close of each day
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS lending_analytics.loan_daily_snapshots (
    -- Partition dimension
    snapshot_date                DATE          NOT NULL,
    loan_id                      UUID          NOT NULL,
    loan_name                    TEXT          NOT NULL,
    currency                     CHAR(3)       NOT NULL,
    status                       TEXT          NOT NULL,
    commitment_amount            NUMERIC(28,8) NOT NULL,
    outstanding_principal        NUMERIC(28,8) NOT NULL,
    accrued_interest             NUMERIC(28,8) NOT NULL DEFAULT 0,
    unamortized_discount         NUMERIC(28,8) NOT NULL DEFAULT 0,
    unamortized_premium          NUMERIC(28,8) NOT NULL DEFAULT 0,
    carrying_value               NUMERIC(28,8) NOT NULL,
    collateral_value             NUMERIC(28,8) NOT NULL DEFAULT 0,
    loan_to_value                NUMERIC(12,6),
    -- Benchmark fixing used on this date (NULL for fixed-rate loans)
    benchmark_rate               NUMERIC(12,8),
    -- Spread (bps converted to decimal) on this date
    spread                       NUMERIC(12,8),
    -- All-in rate = benchmark_rate + spread (or fixed rate)
    all_in_rate                  NUMERIC(12,8),
    credit_rating                TEXT,
    -- Sequence of the last Loan Contract event folded into this snapshot
    source_event_sequence        BIGINT        NOT NULL,
    created_at                   TIMESTAMPTZ   NOT NULL DEFAULT NOW(),

    PRIMARY KEY (snapshot_date, loan_id)
);

SELECT create_hypertable(
    'lending_analytics.loan_daily_snapshots',
    'snapshot_date',
    chunk_time_interval => INTERVAL '1 month',
    if_not_exists => TRUE
);

CREATE INDEX IF NOT EXISTS ix_loan_daily_snapshots_loan
    ON lending_analytics.loan_daily_snapshots (loan_id, snapshot_date DESC);

CREATE INDEX IF NOT EXISTS ix_loan_daily_snapshots_status
    ON lending_analytics.loan_daily_snapshots (status, snapshot_date DESC);

-- ---------------------------------------------------------------------------
-- Portfolio metrics: daily aggregate metrics across all loans
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS lending_analytics.portfolio_metrics (
    -- Partition dimension
    snapshot_date            DATE          NOT NULL PRIMARY KEY,
    total_commitment         NUMERIC(28,8) NOT NULL,
    total_outstanding        NUMERIC(28,8) NOT NULL,
    total_carrying_value     NUMERIC(28,8) NOT NULL,
    total_collateral_value   NUMERIC(28,8) NOT NULL DEFAULT 0,
    -- Counts by status
    loan_count_total         INT           NOT NULL DEFAULT 0,
    loan_count_active        INT           NOT NULL DEFAULT 0,
    loan_count_non_performing INT          NOT NULL DEFAULT 0,
    loan_count_default       INT           NOT NULL DEFAULT 0,
    loan_count_workout       INT           NOT NULL DEFAULT 0,
    loan_count_closed        INT           NOT NULL DEFAULT 0,
    -- Weighted-average all-in yield (NULL when no active loans)
    wavg_all_in_yield        NUMERIC(12,8),
    -- Weighted-average LTV ratio across collateralised loans (NULL if none)
    wavg_ltv                 NUMERIC(12,6),
    created_at               TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

SELECT create_hypertable(
    'lending_analytics.portfolio_metrics',
    'snapshot_date',
    chunk_time_interval => INTERVAL '1 year',
    if_not_exists => TRUE
);

-- ---------------------------------------------------------------------------
-- Continuous aggregate: weekly portfolio summary (materialized view over daily metrics)
-- Refresh policy should be configured separately after creation, e.g.:
--   SELECT add_continuous_aggregate_policy('lending_analytics.portfolio_metrics_weekly',
--       start_offset => INTERVAL '2 weeks', end_offset => INTERVAL '1 hour',
--       schedule_interval => INTERVAL '1 day');
-- ---------------------------------------------------------------------------
CREATE MATERIALIZED VIEW IF NOT EXISTS lending_analytics.portfolio_metrics_weekly
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 week', snapshot_date)    AS week_start,
    MAX(total_outstanding)                   AS peak_outstanding,
    MIN(total_outstanding)                   AS trough_outstanding,
    AVG(total_outstanding)                   AS avg_outstanding,
    MAX(loan_count_active)                   AS peak_active_loans,
    AVG(wavg_all_in_yield)                   AS avg_yield
FROM lending_analytics.portfolio_metrics
GROUP BY time_bucket('1 week', snapshot_date)
WITH NO DATA;
