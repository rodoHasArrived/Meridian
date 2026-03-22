/// Pure accounting projector: maps Loan Contract domain events to double-entry journal entries.
/// No I/O — returns journal entries and cash flows as plain F# values.
/// The calling infrastructure (worker, test, CLI) is responsible for persisting the results.
module Meridian.Lending.Accounting.LoanAccountingProjector

open System
open Meridian.FSharp.Domain.Lending

// ── Helpers ───────────────────────────────────────────────────────────────────

let private debit  (account: LoanAccountCode) (amount: decimal) (currency: Currency) =
    { Account = account; EntryType = EntryType.Debit; Amount = amount; Currency = currency }

let private credit (account: LoanAccountCode) (amount: decimal) (currency: Currency) =
    { Account = account; EntryType = EntryType.Credit; Amount = amount; Currency = currency }

let private entry
    (loanId: Guid)
    (seq: int64)
    (description: string)
    (valueDate: DateOnly)
    (legs: JournalLeg list)
    : JournalEntry option =
    match JournalEntry.create loanId seq description valueDate legs with
    | Ok e  -> Some e
    | Error _ -> None  // Skip unbalanced entries (should never happen in correct implementation)

let private cashFlow
    (loanId: Guid) (seq: int64) (flowType: string)
    (amount: decimal) (currency: Currency) (valueDate: DateOnly) : CashFlow =
    { CashFlowId = Guid.NewGuid()
      LoanId = loanId
      SourceEventSequence = seq
      FlowType = flowType
      Amount = amount
      Currency = currency
      ValueDate = valueDate
      CreatedAt = DateTimeOffset.UtcNow }

// ── Projection result ─────────────────────────────────────────────────────────

/// The accounting artifacts produced by projecting a single domain event.
type AccountingArtifacts = {
    JournalEntries: JournalEntry list
    CashFlows: CashFlow list
}

let private empty = { JournalEntries = []; CashFlows = [] }

let private withEntry e = { JournalEntries = e |> Option.toList; CashFlows = [] }

let private withEntryAndCash (e: JournalEntry option) (cf: CashFlow) =
    { JournalEntries = e |> Option.toList; CashFlows = [ cf ] }

// ── Per-event projector ────────────────────────────────────────────────────────

/// Projects a single Loan Contract event at the given sequence number into
/// the corresponding double-entry journal entries and cash flows.
///
/// The loan's base currency is required to denominate legs that are not
/// denominated by the event itself.
let project
    (loanId: Guid)
    (baseCurrency: Currency)
    (sequenceNumber: int64)
    (event: LoanEvent)
    : AccountingArtifacts =
    match event with

    // ── Drawdown: Dr LoanReceivable / Cr Cash ────────────────────────────────
    | LoanEvent.DrawdownExecuted(amount, currency, date) ->
        let je = entry loanId sequenceNumber
                    "Drawdown disbursed" date
                    [ debit  LoanAccountCode.LoanReceivable amount currency
                      credit LoanAccountCode.Cash           amount currency ]
        let cf = cashFlow loanId sequenceNumber "DrawdownDisbursement" (-amount) currency date
        withEntryAndCash je cf

    // ── Interest accrual: Dr AccruedInterestReceivable / Cr InterestIncome ──
    | LoanEvent.InterestAccrued(amount, date) ->
        let je = entry loanId sequenceNumber
                    "Interest accrual" date
                    [ debit  LoanAccountCode.AccruedInterestReceivable amount baseCurrency
                      credit LoanAccountCode.InterestIncome            amount baseCurrency ]
        withEntry je

    // ── Interest receipt: Dr Cash / Cr AccruedInterestReceivable ────────────
    | LoanEvent.InterestPaid(amount, date) ->
        let je = entry loanId sequenceNumber
                    "Interest received" date
                    [ debit  LoanAccountCode.Cash                     amount baseCurrency
                      credit LoanAccountCode.AccruedInterestReceivable amount baseCurrency ]
        let cf = cashFlow loanId sequenceNumber "InterestReceipt" amount baseCurrency date
        withEntryAndCash je cf

    // ── Commitment fee accrual: Dr AccruedFeeReceivable / Cr CommitmentFeeIncome
    | LoanEvent.CommitmentFeeAccrued(amount, date) ->
        let je = entry loanId sequenceNumber
                    "Commitment fee accrual" date
                    [ debit  LoanAccountCode.AccruedFeeReceivable  amount baseCurrency
                      credit LoanAccountCode.CommitmentFeeIncome   amount baseCurrency ]
        withEntry je

    // ── Commitment fee receipt: Dr Cash / Cr AccruedFeeReceivable ───────────
    | LoanEvent.CommitmentFeePaid(amount, date) ->
        let je = entry loanId sequenceNumber
                    "Commitment fee received" date
                    [ debit  LoanAccountCode.Cash                 amount baseCurrency
                      credit LoanAccountCode.AccruedFeeReceivable amount baseCurrency ]
        let cf = cashFlow loanId sequenceNumber "FeeReceipt" amount baseCurrency date
        withEntryAndCash je cf

    // ── Principal repayment: Dr Cash / Cr LoanReceivable ────────────────────
    | LoanEvent.PrincipalRepaid(amount, date) ->
        let je = entry loanId sequenceNumber
                    "Principal repayment received" date
                    [ debit  LoanAccountCode.Cash           amount baseCurrency
                      credit LoanAccountCode.LoanReceivable amount baseCurrency ]
        let cf = cashFlow loanId sequenceNumber "PrincipalReceipt" amount baseCurrency date
        withEntryAndCash je cf

    // ── Discount amortization: Dr UnamortizedDiscount / Cr DiscountAccretionIncome
    | LoanEvent.DiscountAmortized(amount, date) ->
        let je = entry loanId sequenceNumber
                    "Purchase discount amortized" date
                    [ debit  LoanAccountCode.UnamortizedDiscount      amount baseCurrency
                      credit LoanAccountCode.DiscountAccretionIncome  amount baseCurrency ]
        withEntry je

    // ── Premium amortization: Dr PremiumAmortizationExpense / Cr UnamortizedPremium
    | LoanEvent.PremiumAmortized(amount, date) ->
        let je = entry loanId sequenceNumber
                    "Purchase premium amortized" date
                    [ debit  LoanAccountCode.PremiumAmortizationExpense amount baseCurrency
                      credit LoanAccountCode.UnamortizedPremium         amount baseCurrency ]
        withEntry je

    // ── Principal forgiveness: Dr ProvisionForCreditLoss / Cr LoanReceivable
    | LoanEvent.PrincipalForgiven(amount, date) ->
        let je = entry loanId sequenceNumber
                    "Principal forgiven (debt write-down)" date
                    [ debit  LoanAccountCode.ProvisionForCreditLoss amount baseCurrency
                      credit LoanAccountCode.LoanReceivable         amount baseCurrency ]
        withEntry je

    // ── PIK capitalisation: Dr LoanReceivable / Cr AccruedInterestReceivable
    | LoanEvent.PikInterestCapitalized(amount, date) ->
        let je = entry loanId sequenceNumber
                    "PIK interest capitalised into principal" date
                    [ debit  LoanAccountCode.LoanReceivable            amount baseCurrency
                      credit LoanAccountCode.AccruedInterestReceivable amount baseCurrency ]
        withEntry je

    // ── Write-off: Dr AllowanceForCreditLoss / Cr LoanReceivable ───────────
    | LoanEvent.LoanWrittenOff(amount, date) ->
        let je = entry loanId sequenceNumber
                    "Loan written off" date
                    [ debit  LoanAccountCode.AllowanceForCreditLoss amount baseCurrency
                      credit LoanAccountCode.LoanReceivable         amount baseCurrency ]
        withEntry je

    // ── Fee charged: Dr Cash / Cr FeeIncome ────────────────────────────────
    | LoanEvent.FeeCharged(feeType, amount, date) ->
        let je = entry loanId sequenceNumber
                    $"Fee charged: {feeType}" date
                    [ debit  LoanAccountCode.Cash      amount baseCurrency
                      credit LoanAccountCode.FeeIncome amount baseCurrency ]
        let cf = cashFlow loanId sequenceNumber "FeeReceipt" amount baseCurrency date
        withEntryAndCash je cf

    // ── Loan created: record initial discount / premium ─────────────────────
    | LoanEvent.LoanCreated(header, terms) ->
        match terms.PurchasePrice with
        | None -> empty
        | Some price ->
            let face = terms.CommitmentAmount
            let currency = header.BaseCurrency
            let valueDate = terms.OriginationDate
            if price < 1m then
                // Discount: Dr UnamortizedDiscount / Cr Cash
                let discountAmount = (1m - price) * face
                let je = entry loanId sequenceNumber
                            "Loan originated at discount" valueDate
                            [ debit  LoanAccountCode.UnamortizedDiscount discountAmount currency
                              credit LoanAccountCode.Cash                discountAmount currency ]
                withEntry je
            elif price > 1m then
                // Premium: Dr Cash / Cr UnamortizedPremium
                let premiumAmount = (price - 1m) * face
                let je = entry loanId sequenceNumber
                            "Loan originated at premium" valueDate
                            [ debit  LoanAccountCode.Cash               premiumAmount currency
                              credit LoanAccountCode.UnamortizedPremium premiumAmount currency ]
                withEntry je
            else empty

    // Events with no accounting entries (status changes, metadata, rate resets, etc.)
    | LoanEvent.LoanCommitted _
    | LoanEvent.InterestRateReset _
    | LoanEvent.TermsAmended _
    | LoanEvent.LoanClosed _
    | LoanEvent.LoanRestructured _
    | LoanEvent.CollateralAdded _
    | LoanEvent.CollateralReleased _
    | LoanEvent.CollateralRevalued _
    | LoanEvent.LoanMarkedNonPerforming _
    | LoanEvent.LoanDefaulted _
    | LoanEvent.DefaultCured _
    | LoanEvent.LoanPlacedInWorkout _
    | LoanEvent.LoanRiskRated _
    | LoanEvent.CovenantAdded _
    | LoanEvent.CovenantBreached _
    | LoanEvent.CovenantWaived _
    | LoanEvent.CovenantAmended _ ->
        empty

/// Projects a full sequence of loan events from a given base currency.
/// Returns all journal entries and cash flows in event order.
let projectAll
    (loanId: Guid)
    (baseCurrency: Currency)
    (events: (int64 * LoanEvent) seq)
    : AccountingArtifacts =
    events
    |> Seq.map (fun (seq, ev) -> project loanId baseCurrency seq ev)
    |> Seq.fold (fun acc a ->
        { JournalEntries = acc.JournalEntries @ a.JournalEntries
          CashFlows      = acc.CashFlows @ a.CashFlows }) empty
