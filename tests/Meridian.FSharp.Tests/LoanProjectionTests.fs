/// Unit tests for the LoanProjection read-model projection module.
module Meridian.FSharp.Tests.LoanProjectionTests

open System
open Xunit
open FsUnit.Xunit
open Meridian.FSharp.Domain.Lending
open Meridian.Lending.Projections.LoanProjection

// ── Helpers ───────────────────────────────────────────────────────────────────

let private makeHeader () : LoanHeader =
    { SecurityId     = Guid.NewGuid()
      Name           = "Test Term Loan"
      BaseCurrency   = Currency.USD
      EffectiveDate  = DateOnly(2025, 1, 15) }

let private makeTerms () : DirectLendingTerms =
    { OriginationDate         = DateOnly(2025, 1, 15)
      MaturityDate            = DateOnly(2028, 1, 15)
      CommitmentAmount        = 5_000_000m
      CommitmentFeeRate       = Some 0.005m
      InterestRate            = Some 0.08m
      InterestIndex           = None
      SpreadBps               = None
      PaymentFrequencyMonths  = 3
      AmortizationType        = AmortizationType.BulletMaturity
      DayCountConvention      = DayCountConvention.Actual360
      PurchasePrice           = None
      CovenantsJson           = None
      InterestOnlyMonths      = 0
      GracePeriodDays         = None
      EffectiveRateFloor      = None
      EffectiveRateCap        = None
      PrepaymentPenaltyRate   = None }

let private createState (extraEvents: LoanEvent list) : LoanState =
    let initial = [ LoanEvent.LoanCreated(makeHeader(), makeTerms()) ]
    match LoanAggregate.rebuild (initial @ extraEvents) with
    | Some s -> s
    | None   -> failwith "Failed to rebuild state"

// ── project — basic field mapping ─────────────────────────────────────────────

[<Fact>]
let ``project maps LoanId correctly`` () =
    let id    = Guid.NewGuid()
    let state = createState []
    let row   = project id state 1L
    row.LoanId |> should equal id

[<Fact>]
let ``project maps Name from LoanHeader`` () =
    let state = createState []
    let row   = project (Guid.NewGuid()) state 0L
    row.Name |> should equal "Test Term Loan"

[<Fact>]
let ``project maps BaseCurrency as ISO string`` () =
    let state = createState []
    let row   = project (Guid.NewGuid()) state 0L
    row.BaseCurrency |> should equal "USD"

[<Fact>]
let ``project maps CommitmentAmount from Terms`` () =
    let state = createState []
    let row   = project (Guid.NewGuid()) state 0L
    row.CommitmentAmount |> should equal 5_000_000m

[<Fact>]
let ``project maps OriginationDate and MaturityDate from Terms`` () =
    let state = createState []
    let row   = project (Guid.NewGuid()) state 0L
    row.OriginationDate |> should equal (DateOnly(2025, 1, 15))
    row.MaturityDate    |> should equal (DateOnly(2028, 1, 15))

[<Fact>]
let ``project maps LastEventSequence`` () =
    let state = createState []
    let row   = project (Guid.NewGuid()) state 42L
    row.LastEventSequence |> should equal 42L

[<Fact>]
let ``project maps Version from LoanState`` () =
    let state = createState []
    let row   = project (Guid.NewGuid()) state 0L
    row.Version |> should equal state.Version

// ── project — status mapping ───────────────────────────────────────────────────

[<Fact>]
let ``project maps Pending status to string`` () =
    let state = createState []
    let row   = project (Guid.NewGuid()) state 0L
    row.Status |> should equal "Pending"

[<Fact>]
let ``project maps Committed status to string`` () =
    let state = createState [ LoanEvent.LoanCommitted(5_000_000m, Currency.USD) ]
    let row   = project (Guid.NewGuid()) state 0L
    row.Status |> should equal "Committed"

[<Fact>]
let ``project maps Active status to string`` () =
    let state = createState [
        LoanEvent.LoanCommitted(5_000_000m, Currency.USD)
        LoanEvent.DrawdownExecuted(1_000_000m, Currency.USD, DateOnly(2025, 1, 15))
    ]
    let row = project (Guid.NewGuid()) state 0L
    row.Status |> should equal "Active"

[<Fact>]
let ``project maps Closed status to string`` () =
    let state = createState [
        LoanEvent.LoanCommitted(5_000_000m, Currency.USD)
        LoanEvent.DrawdownExecuted(1_000_000m, Currency.USD, DateOnly(2025, 1, 15))
        LoanEvent.PrincipalRepaid(1_000_000m, DateOnly(2028, 1, 15))
        LoanEvent.LoanClosed(DateOnly(2028, 1, 15))
    ]
    let row = project (Guid.NewGuid()) state 0L
    row.Status |> should equal "Closed"

// ── project — computed financials ─────────────────────────────────────────────

[<Fact>]
let ``project computes CarryingValue via LoanService`` () =
    let state = createState [
        LoanEvent.LoanCommitted(5_000_000m, Currency.USD)
        LoanEvent.DrawdownExecuted(2_000_000m, Currency.USD, DateOnly(2025, 1, 15))
    ]
    let row      = project (Guid.NewGuid()) state 0L
    let expected = LoanService.carryingValue state
    row.CarryingValue |> should equal expected

[<Fact>]
let ``project computes CollateralValue via LoanService`` () =
    let state = createState []
    let row   = project (Guid.NewGuid()) state 0L
    row.CollateralValue |> should equal 0m

[<Fact>]
let ``project returns None for LoanToValue when no collateral`` () =
    let state = createState [
        LoanEvent.LoanCommitted(5_000_000m, Currency.USD)
        LoanEvent.DrawdownExecuted(1_000_000m, Currency.USD, DateOnly(2025, 1, 15))
    ]
    let row = project (Guid.NewGuid()) state 0L
    row.LoanToValue |> should equal None

// ── project — credit rating ────────────────────────────────────────────────────

[<Fact>]
let ``project maps None CreditRating to None`` () =
    let state = createState []
    let row   = project (Guid.NewGuid()) state 0L
    row.CreditRating      |> should equal None
    row.IsInvestmentGrade |> should equal None

[<Fact>]
let ``project maps investment-grade CreditRating correctly`` () =
    let state = createState [
        LoanEvent.LoanRiskRated(CreditRating.BBB, "S&P", DateOnly(2025, 3, 1))
    ]
    let row = project (Guid.NewGuid()) state 0L
    row.CreditRating      |> should equal (Some "BBB")
    row.IsInvestmentGrade |> should equal (Some true)

[<Fact>]
let ``project maps sub-investment-grade CreditRating correctly`` () =
    let state = createState [
        LoanEvent.LoanRiskRated(CreditRating.BB, "Moody's", DateOnly(2025, 3, 1))
    ]
    let row = project (Guid.NewGuid()) state 0L
    row.CreditRating      |> should equal (Some "BB")
    row.IsInvestmentGrade |> should equal (Some false)

[<Fact>]
let ``project sets OutstandingPrincipal after drawdown`` () =
    let state = createState [
        LoanEvent.LoanCommitted(5_000_000m, Currency.USD)
        LoanEvent.DrawdownExecuted(2_500_000m, Currency.USD, DateOnly(2025, 1, 15))
    ]
    let row = project (Guid.NewGuid()) state 0L
    row.OutstandingPrincipal |> should equal 2_500_000m
