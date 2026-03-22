/// Pure read-model projection for a single loan.
/// No I/O — maps a LoanState snapshot to a flat LoanPositionRow record.
module Meridian.Lending.Projections.LoanProjection

open System
open Meridian.FSharp.Domain.Lending

/// Flattened read-model row for a single loan, written to lending.loan_positions.
type LoanPositionRow = {
    LoanId: Guid
    Name: string
    BaseCurrency: string
    Status: string
    OriginationDate: DateOnly
    MaturityDate: DateOnly
    CommitmentAmount: decimal
    OutstandingPrincipal: decimal
    AccruedInterestUnpaid: decimal
    AccruedCommitmentFeeUnpaid: decimal
    UnamortizedDiscount: decimal
    UnamortizedPremium: decimal
    CarryingValue: decimal
    CollateralValue: decimal
    LoanToValue: decimal option
    CreditRating: string option
    IsInvestmentGrade: bool option
    LastEventSequence: int64
    Version: int64
    UpdatedAt: DateTimeOffset
}

let private statusToString (s: LoanStatus) =
    match s with
    | LoanStatus.Pending       -> "Pending"
    | LoanStatus.Committed     -> "Committed"
    | LoanStatus.Active        -> "Active"
    | LoanStatus.NonPerforming -> "NonPerforming"
    | LoanStatus.Default       -> "Default"
    | LoanStatus.Workout       -> "Workout"
    | LoanStatus.Closed        -> "Closed"

/// Projects a LoanState and its last known event sequence number into a LoanPositionRow.
let project (loanId: Guid) (state: LoanState) (lastSequence: int64) : LoanPositionRow =
    {
        LoanId                     = loanId
        Name                       = state.Header.Name
        BaseCurrency               = state.Header.BaseCurrency.ToString()
        Status                     = statusToString state.Status
        OriginationDate            = state.Terms.OriginationDate
        MaturityDate               = state.Terms.MaturityDate
        CommitmentAmount           = state.Terms.CommitmentAmount
        OutstandingPrincipal       = state.OutstandingPrincipal
        AccruedInterestUnpaid      = state.AccruedInterestUnpaid
        AccruedCommitmentFeeUnpaid = state.AccruedCommitmentFeeUnpaid
        UnamortizedDiscount        = state.UnamortizedDiscount
        UnamortizedPremium         = state.UnamortizedPremium
        CarryingValue              = LoanService.carryingValue state
        CollateralValue            = LoanService.totalCollateralValue state
        LoanToValue                = LoanService.loanToValue state
        CreditRating               = state.CreditRating |> Option.map (fun r -> r.ToString())
        IsInvestmentGrade          = state.CreditRating |> Option.map (fun r -> r.IsInvestmentGrade)
        LastEventSequence          = lastSequence
        Version                    = state.Version
        UpdatedAt                  = DateTimeOffset.UtcNow
    }
