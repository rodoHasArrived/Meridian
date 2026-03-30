-- =============================================================================
-- Relational accounting and cash tables
-- =============================================================================
-- Flyway naming: V5__accounting.sql
--
-- Design:
--   lending.journal_entries  — balanced double-entry journal; every economic event
--                              produces one header row + two or more leg rows.
--   lending.journal_legs     — individual debit/credit legs of a journal entry.
--   lending.cash_ledger      — cash flow record for each actual cash movement.
--   lending.accrual_balances — current snapshot of accrual-basis balances per loan.
--
-- Design principles:
--   - Accounting facts are posted rows with event lineage (SourceEventSequence).
--   - No destructive updates: corrections are made via reversing / adjusting entries.
--   - Read models (accrual_balances) must be rebuildable from journal_legs in replay-safe mode.
-- =============================================================================

-- ---------------------------------------------------------------------------
-- Journal entries (header)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS lending.journal_entries (
    id                      BIGSERIAL     PRIMARY KEY,
    entry_id                UUID          NOT NULL UNIQUE DEFAULT gen_random_uuid(),
    loan_id                 UUID          NOT NULL,
    -- Sequence number of the domain event that triggered this entry.
    source_event_sequence   BIGINT        NOT NULL,
    -- "LoanContract" | "LoanServicing"
    source_aggregate_type   TEXT          NOT NULL DEFAULT 'LoanContract',
    description             TEXT          NOT NULL,
    value_date              DATE          NOT NULL,
    created_at              TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    -- NULL = active; NOT NULL = reversed (points to the reversing entry).
    reversed_by_entry_id    UUID
);

CREATE INDEX IF NOT EXISTS ix_journal_entries_loan
    ON lending.journal_entries (loan_id, value_date ASC);

CREATE INDEX IF NOT EXISTS ix_journal_entries_event_seq
    ON lending.journal_entries (loan_id, source_event_sequence ASC);

-- ---------------------------------------------------------------------------
-- Journal legs (debit / credit lines)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS lending.journal_legs (
    id           BIGSERIAL    PRIMARY KEY,
    entry_id     UUID         NOT NULL REFERENCES lending.journal_entries(entry_id) ON DELETE CASCADE,
    -- Account code from the chart of accounts (e.g. 'LoanReceivable', 'Cash').
    account_code TEXT         NOT NULL,
    -- 'Debit' | 'Credit'
    entry_type   TEXT         NOT NULL,
    amount       NUMERIC(28,8) NOT NULL CHECK (amount > 0),
    currency     CHAR(3)      NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_journal_legs_entry
    ON lending.journal_legs (entry_id);

CREATE INDEX IF NOT EXISTS ix_journal_legs_account
    ON lending.journal_legs (account_code, entry_type);

-- ---------------------------------------------------------------------------
-- Cash ledger: actual cash receipts and disbursements
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS lending.cash_ledger (
    id                    BIGSERIAL     PRIMARY KEY,
    cash_flow_id          UUID          NOT NULL UNIQUE DEFAULT gen_random_uuid(),
    loan_id               UUID          NOT NULL,
    source_event_sequence BIGINT        NOT NULL,
    -- 'DrawdownDisbursement' | 'InterestReceipt' | 'PrincipalReceipt' | 'FeeReceipt' | 'PikSettlement'
    flow_type             TEXT          NOT NULL,
    -- Positive = inflow to lender (receipt); negative = outflow (disbursement).
    amount                NUMERIC(28,8) NOT NULL,
    currency              CHAR(3)       NOT NULL,
    value_date            DATE          NOT NULL,
    created_at            TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_cash_ledger_loan
    ON lending.cash_ledger (loan_id, value_date ASC);

-- ---------------------------------------------------------------------------
-- Accrual balances: current-state snapshot, updated by the accrual worker
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS lending.accrual_balances (
    loan_id                      UUID          PRIMARY KEY,
    accrued_interest_unpaid      NUMERIC(28,8) NOT NULL DEFAULT 0,
    accrued_commitment_fee_unpaid NUMERIC(28,8) NOT NULL DEFAULT 0,
    unamortized_discount         NUMERIC(28,8) NOT NULL DEFAULT 0,
    unamortized_premium          NUMERIC(28,8) NOT NULL DEFAULT 0,
    outstanding_principal        NUMERIC(28,8) NOT NULL DEFAULT 0,
    -- The last event sequence that was folded into this row.
    last_event_sequence          BIGINT        NOT NULL DEFAULT 0,
    updated_at                   TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);
