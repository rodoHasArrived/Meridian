-- V2: Read-model projection table for the direct-lending domain.
-- Kept in sync with the event stream by PostgresLoanPositionProjector.

CREATE TABLE IF NOT EXISTS lending.loan_positions (
    loan_id                       UUID          PRIMARY KEY,
    name                          TEXT          NOT NULL,
    base_currency                 VARCHAR(10)   NOT NULL,
    status                        VARCHAR(30)   NOT NULL,
    origination_date              DATE          NOT NULL,
    maturity_date                 DATE          NOT NULL,
    commitment_amount             NUMERIC(19,4) NOT NULL,
    outstanding_principal         NUMERIC(19,4) NOT NULL DEFAULT 0,
    accrued_interest_unpaid       NUMERIC(19,4) NOT NULL DEFAULT 0,
    accrued_commitment_fee_unpaid NUMERIC(19,4) NOT NULL DEFAULT 0,
    unamortized_discount          NUMERIC(19,4) NOT NULL DEFAULT 0,
    unamortized_premium           NUMERIC(19,4) NOT NULL DEFAULT 0,
    carrying_value                NUMERIC(19,4) NOT NULL DEFAULT 0,
    collateral_value              NUMERIC(19,4) NOT NULL DEFAULT 0,
    loan_to_value                 NUMERIC(10,6),
    credit_rating                 VARCHAR(5),
    is_investment_grade           BOOLEAN,
    last_event_sequence           BIGINT        NOT NULL DEFAULT 0,
    version                       BIGINT        NOT NULL DEFAULT 0,
    updated_at                    TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_loan_positions_status   ON lending.loan_positions (status);
CREATE INDEX IF NOT EXISTS ix_loan_positions_currency ON lending.loan_positions (base_currency);
CREATE INDEX IF NOT EXISTS ix_loan_positions_maturity ON lending.loan_positions (maturity_date);
