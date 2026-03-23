/// Pure types for TimescaleDB analytics layer.
/// These are DTO records used by the daily snapshot worker to populate
/// lending_analytics.loan_daily_snapshots, lending_analytics.benchmark_fixings,
/// and lending_analytics.portfolio_metrics hypertables.
module Meridian.Lending.Analytics.BenchmarkFixingTypes

open System
open Meridian.FSharp.Domain.Lending

// ── Benchmark fixings ─────────────────────────────────────────────────────────

/// A reference rate fixing for a named index on a specific date.
[<CLIMutable>]
type BenchmarkFixing = {
    FixingDate: DateOnly
    /// Standard index name: "SOFR", "EURIBOR3M", "SONIA", "LIBOR3M", etc.
    IndexName: string
    /// Fixing rate as a decimal (e.g. 0.0532 = 5.32 %).
    Rate: decimal
    /// Source of the fixing: "CME", "ECB", "BOE", "MANUAL", etc.
    Source: string
}

// ── Daily loan snapshot ───────────────────────────────────────────────────────

/// Daily per-loan economic snapshot written to lending_analytics.loan_daily_snapshots.
[<CLIMutable>]
type LoanDailySnapshot = {
    SnapshotDate: DateOnly
    LoanId: Guid
    LoanName: string
    Currency: string
    Status: string
    CommitmentAmount: decimal
    OutstandingPrincipal: decimal
    AccruedInterest: decimal
    UnamortizedDiscount: decimal
    UnamortizedPremium: decimal
    CarryingValue: decimal
    CollateralValue: decimal
    LoanToValue: decimal option
    BenchmarkRate: decimal option
    Spread: decimal option
    AllInRate: decimal option
    CreditRating: string option
    SourceEventSequence: int64
}

/// Projects a LoanState and optional benchmark fixing into a LoanDailySnapshot.
let projectSnapshot
    (snapshotDate: DateOnly)
    (loanId: Guid)
    (state: LoanState)
    (lastSequence: int64)
    (benchmarkRate: decimal option)
    : LoanDailySnapshot =
    let spreadDecimal = state.Terms.SpreadBps |> Option.map (fun bps -> bps / 10_000m)
    let allInRate =
        match state.Terms.InterestRate with
        | Some r -> Some r
        | None ->
            match benchmarkRate, spreadDecimal with
            | Some b, Some s -> Some (b + s)
            | _ -> None
    {
        SnapshotDate         = snapshotDate
        LoanId               = loanId
        LoanName             = state.Header.Name
        Currency             = state.Header.BaseCurrency.ToString()
        Status               = state.Status.ToString()
        CommitmentAmount     = state.Terms.CommitmentAmount
        OutstandingPrincipal = state.OutstandingPrincipal
        AccruedInterest      = state.AccruedInterestUnpaid
        UnamortizedDiscount  = state.UnamortizedDiscount
        UnamortizedPremium   = state.UnamortizedPremium
        CarryingValue        = LoanService.carryingValue state
        CollateralValue      = LoanService.totalCollateralValue state
        LoanToValue          = LoanService.loanToValue state
        BenchmarkRate        = benchmarkRate
        Spread               = spreadDecimal
        AllInRate            = allInRate
        CreditRating         = state.CreditRating |> Option.map (fun r -> r.ToString())
        SourceEventSequence  = lastSequence
    }

// ── Portfolio metrics ─────────────────────────────────────────────────────────

/// Daily portfolio-level aggregate metrics written to lending_analytics.portfolio_metrics.
[<CLIMutable>]
type PortfolioMetrics = {
    SnapshotDate: DateOnly
    TotalCommitment: decimal
    TotalOutstanding: decimal
    TotalCarryingValue: decimal
    TotalCollateralValue: decimal
    LoanCountTotal: int
    LoanCountActive: int
    LoanCountNonPerforming: int
    LoanCountDefault: int
    LoanCountWorkout: int
    LoanCountClosed: int
    WavgAllInYield: decimal option
    WavgLtv: decimal option
}

/// Aggregates a collection of daily snapshots into portfolio-level metrics.
let aggregatePortfolioMetrics
    (snapshotDate: DateOnly)
    (snapshots: LoanDailySnapshot list)
    : PortfolioMetrics =
    let total = snapshots.Length
    let byStatus s = snapshots |> List.filter (fun x -> x.Status = s) |> List.length
    let sumOutstanding = snapshots |> List.sumBy _.OutstandingPrincipal

    // Weighted-average all-in yield: weight = outstanding principal
    let wavgYield =
        let withYield = snapshots |> List.choose (fun s ->
            match s.AllInRate with
            | Some r when s.OutstandingPrincipal > 0m -> Some (r, s.OutstandingPrincipal)
            | _ -> None)
        if withYield.IsEmpty then None
        else
            let weightedSum = withYield |> List.sumBy (fun (r, w) -> r * w)
            let totalWeight  = withYield |> List.sumBy snd
            if totalWeight = 0m then None else Some (weightedSum / totalWeight)

    // Weighted-average LTV: weight = outstanding principal
    let wavgLtv =
        let withLtv = snapshots |> List.choose (fun s ->
            match s.LoanToValue with
            | Some ltv when s.OutstandingPrincipal > 0m -> Some (ltv, s.OutstandingPrincipal)
            | _ -> None)
        if withLtv.IsEmpty then None
        else
            let weightedSum = withLtv |> List.sumBy (fun (ltv, w) -> ltv * w)
            let totalWeight  = withLtv |> List.sumBy snd
            if totalWeight = 0m then None else Some (weightedSum / totalWeight)

    {
        SnapshotDate           = snapshotDate
        TotalCommitment        = snapshots |> List.sumBy _.CommitmentAmount
        TotalOutstanding       = sumOutstanding
        TotalCarryingValue     = snapshots |> List.sumBy _.CarryingValue
        TotalCollateralValue   = snapshots |> List.sumBy _.CollateralValue
        LoanCountTotal         = total
        LoanCountActive        = byStatus "Active"
        LoanCountNonPerforming = byStatus "NonPerforming"
        LoanCountDefault       = byStatus "Default"
        LoanCountWorkout       = byStatus "Workout"
        LoanCountClosed        = byStatus "Closed"
        WavgAllInYield         = wavgYield
        WavgLtv                = wavgLtv
    }
