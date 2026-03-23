/// High-level repository for Loan Servicing aggregates.
/// Mirrors the structure of LoanContractRepository.fs.
module Meridian.Lending.LoanServicingRepository

open System
open Meridian.FSharp.Domain.Lending
open Meridian.Lending.EventStore.ILoanEventStore
open Meridian.Lending.LoanServicingAggregate

[<Literal>]
let private SnapshotEvery = 25L

[<RequireQualifiedAccess>]
type ServicingCommandResult =
    | Success of events: ServicingEvent list * newSequence: int64
    | DomainError of message: string
    | ConflictError of ConcurrencyError

/// Loads the current servicing aggregate state for a loan.
/// Uses the most recent snapshot and replays only subsequent events.
let load (store: ILoanEventStore) (loanId: Guid) : LoanServicingAggregate * int64 =
    match store.GetSnapshot loanId with
    | Some snap ->
        let baseState = deserializeState snap.StateJson
        let baseAgg = { Id = loanId; State = Some baseState }
        let laterEvents = store.GetEventsAfter(loanId, snap.SequenceNumber)
        let finalAgg =
            laterEvents
            |> List.fold (fun (agg: LoanServicingAggregate) ev ->
                agg.Evolve(deserializeEvent ev.EventJson)) baseAgg
        let currentSeq =
            if laterEvents.IsEmpty then snap.SequenceNumber
            else (List.last laterEvents).SequenceNumber
        finalAgg, currentSeq
    | None ->
        let allEvents = store.GetEvents loanId
        let agg =
            allEvents
            |> List.fold (fun (agg: LoanServicingAggregate) ev ->
                agg.Evolve(deserializeEvent ev.EventJson))
                         (LoanServicingAggregate.Zero loanId)
        let currentSeq =
            if allEvents.IsEmpty then 0L
            else (List.last allEvents).SequenceNumber
        agg, currentSeq

/// Executes a command against the Loan Servicing aggregate.
let execute
    (store: ILoanEventStore)
    (loanId: Guid)
    (command: ServicingCommand)
    : ServicingCommandResult =
    let agg, currentSeq = load store loanId
    match agg.Execute command with
    | Error msg -> ServicingCommandResult.DomainError msg
    | Ok [] -> ServicingCommandResult.Success([], currentSeq)
    | Ok newEvents ->
        let eventJsons = newEvents |> List.map serializeEvent
        match store.AppendEvents(loanId, currentSeq, eventJsons) with
        | ConcurrencyConflict err -> ServicingCommandResult.ConflictError err
        | Appended newSeq ->
            let prevBucket = currentSeq / SnapshotEvery
            let newBucket  = newSeq / SnapshotEvery
            if newBucket > prevBucket then
                let updatedAgg =
                    newEvents |> List.fold (fun (a: LoanServicingAggregate) e -> a.Evolve e) agg
                match updatedAgg.State with
                | Some state -> store.SaveSnapshot(loanId, newSeq, serializeState state)
                | None -> ()
            ServicingCommandResult.Success(newEvents, newSeq)

/// Returns the current visible state of the servicing aggregate, or None.
let getState (store: ILoanEventStore) (loanId: Guid) : ServicingState option =
    let agg, _ = load store loanId
    agg.State

/// Returns all raw servicing events for a loan, in order.
let getEvents (store: ILoanEventStore) (loanId: Guid) : ServicingEvent list =
    store.GetEvents loanId
    |> List.map (fun ev -> deserializeEvent ev.EventJson)
