/// PostgreSQL-backed projector that writes LoanPositionRow records into the
/// lending.loan_positions read-model table.
///
/// All public functions are idempotent — safe to call multiple times for the same loan.
module Meridian.Lending.Projections.PostgresLoanPositionProjector

open System
open Npgsql
open Meridian.Lending.EventStore.ILoanEventStore
open Meridian.Lending.LoanContractRepository
open Meridian.Lending.Projections.LoanProjection

let private addOpt (cmd: Npgsql.NpgsqlCommand) (name: string) (value: 'a option) =
    match value with
    | Some v -> cmd.Parameters.AddWithValue(name, v :> obj) |> ignore
    | None   -> cmd.Parameters.AddWithValue(name, DBNull.Value :> obj) |> ignore

let private upsertRow (conn: NpgsqlConnection) (row: LoanPositionRow) =
    use cmd = conn.CreateCommand()
    cmd.CommandText <- """
        INSERT INTO lending.loan_positions (
            loan_id, name, base_currency, status,
            origination_date, maturity_date, commitment_amount,
            outstanding_principal, accrued_interest_unpaid, accrued_commitment_fee_unpaid,
            unamortized_discount, unamortized_premium, carrying_value,
            collateral_value, loan_to_value, credit_rating, is_investment_grade,
            last_event_sequence, version, updated_at
        ) VALUES (
            @loanId, @name, @baseCurrency, @status,
            @originationDate, @maturityDate, @commitmentAmount,
            @outstandingPrincipal, @accruedInterestUnpaid, @accruedCommitmentFeeUnpaid,
            @unamortizedDiscount, @unamortizedPremium, @carryingValue,
            @collateralValue, @loanToValue, @creditRating, @isInvestmentGrade,
            @lastEventSequence, @version, @updatedAt
        )
        ON CONFLICT (loan_id) DO UPDATE SET
            name                          = EXCLUDED.name,
            base_currency                 = EXCLUDED.base_currency,
            status                        = EXCLUDED.status,
            origination_date              = EXCLUDED.origination_date,
            maturity_date                 = EXCLUDED.maturity_date,
            commitment_amount             = EXCLUDED.commitment_amount,
            outstanding_principal         = EXCLUDED.outstanding_principal,
            accrued_interest_unpaid       = EXCLUDED.accrued_interest_unpaid,
            accrued_commitment_fee_unpaid = EXCLUDED.accrued_commitment_fee_unpaid,
            unamortized_discount          = EXCLUDED.unamortized_discount,
            unamortized_premium           = EXCLUDED.unamortized_premium,
            carrying_value                = EXCLUDED.carrying_value,
            collateral_value              = EXCLUDED.collateral_value,
            loan_to_value                 = EXCLUDED.loan_to_value,
            credit_rating                 = EXCLUDED.credit_rating,
            is_investment_grade           = EXCLUDED.is_investment_grade,
            last_event_sequence           = EXCLUDED.last_event_sequence,
            version                       = EXCLUDED.version,
            updated_at                    = EXCLUDED.updated_at
    """
    cmd.Parameters.AddWithValue("loanId",                     row.LoanId)                  |> ignore
    cmd.Parameters.AddWithValue("name",                       row.Name)                    |> ignore
    cmd.Parameters.AddWithValue("baseCurrency",               row.BaseCurrency)            |> ignore
    cmd.Parameters.AddWithValue("status",                     row.Status)                  |> ignore
    cmd.Parameters.AddWithValue("originationDate",            row.OriginationDate)         |> ignore
    cmd.Parameters.AddWithValue("maturityDate",               row.MaturityDate)            |> ignore
    cmd.Parameters.AddWithValue("commitmentAmount",           row.CommitmentAmount)        |> ignore
    cmd.Parameters.AddWithValue("outstandingPrincipal",       row.OutstandingPrincipal)    |> ignore
    cmd.Parameters.AddWithValue("accruedInterestUnpaid",      row.AccruedInterestUnpaid)   |> ignore
    cmd.Parameters.AddWithValue("accruedCommitmentFeeUnpaid", row.AccruedCommitmentFeeUnpaid) |> ignore
    cmd.Parameters.AddWithValue("unamortizedDiscount",        row.UnamortizedDiscount)     |> ignore
    cmd.Parameters.AddWithValue("unamortizedPremium",         row.UnamortizedPremium)      |> ignore
    cmd.Parameters.AddWithValue("carryingValue",              row.CarryingValue)           |> ignore
    cmd.Parameters.AddWithValue("collateralValue",            row.CollateralValue)         |> ignore
    addOpt cmd "loanToValue"       row.LoanToValue
    addOpt cmd "creditRating"      row.CreditRating
    addOpt cmd "isInvestmentGrade" row.IsInvestmentGrade
    cmd.Parameters.AddWithValue("lastEventSequence",          row.LastEventSequence)       |> ignore
    cmd.Parameters.AddWithValue("version",                    row.Version)                 |> ignore
    cmd.Parameters.AddWithValue("updatedAt",                  row.UpdatedAt)               |> ignore
    cmd.ExecuteNonQuery() |> ignore

/// Returns all distinct loan IDs from the event log.
let getAllLoanIds (connectionString: string) : Guid list =
    use conn = new NpgsqlConnection(connectionString)
    conn.Open()
    use cmd = conn.CreateCommand()
    cmd.CommandText <- "SELECT DISTINCT aggregate_id FROM lending.loan_contract_events ORDER BY aggregate_id"
    use reader = cmd.ExecuteReader()
    let results = ResizeArray()
    while reader.Read() do
        results.Add(reader.GetGuid(0))
    results |> Seq.toList

/// Projects a single loan's current state into the loan_positions read model.
/// Idempotent — safe to call multiple times.
let projectLoan (store: ILoanEventStore) (connectionString: string) (loanId: Guid) : unit =
    let agg, lastSeq = load store loanId
    match agg.State with
    | None -> ()
    | Some state ->
        let row = project loanId state lastSeq
        use conn = new NpgsqlConnection(connectionString)
        conn.Open()
        upsertRow conn row

/// Projects all loans into the loan_positions read model.
/// Use this to rebuild the entire read model from scratch, or to catch up after downtime.
let projectAll (store: ILoanEventStore) (connectionString: string) : unit =
    let loanIds = getAllLoanIds connectionString
    for loanId in loanIds do
        projectLoan store connectionString loanId
