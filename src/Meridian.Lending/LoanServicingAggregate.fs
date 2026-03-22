/// Sharpino-compatible wrapper for the Loan Servicing aggregate.
/// Mirrors the structure of LoanContractAggregate.fs with FsPickler JSON serialization.
module Meridian.Lending.LoanServicingAggregate

open System
open MBrace.FsPickler.Json
open Meridian.FSharp.Domain.Lending

// ── Serializer ────────────────────────────────────────────────────────────────

let private serializer = JsonSerializer(indent = false, omitHeader = true)

let serializeEvent (event: ServicingEvent) : string =
    use sw = new System.IO.StringWriter()
    serializer.Serialize(sw, event)
    sw.ToString()

let deserializeEvent (json: string) : ServicingEvent =
    use sr = new System.IO.StringReader(json)
    serializer.Deserialize<ServicingEvent>(sr)

let serializeState (state: ServicingState) : string =
    use sw = new System.IO.StringWriter()
    serializer.Serialize(sw, state)
    sw.ToString()

let deserializeState (json: string) : ServicingState =
    use sr = new System.IO.StringReader(json)
    serializer.Deserialize<ServicingState>(sr)

// ── Aggregate wrapper ─────────────────────────────────────────────────────────

/// Sharpino-pattern aggregate for the Loan Servicing write side.
type LoanServicingAggregate = {
    Id: Guid
    State: ServicingState option
} with
    static member StorageName = "_loan_servicing"
    static member Version = "_v1"

    static member Zero(id: Guid) : LoanServicingAggregate =
        { Id = id; State = None }

    member this.Evolve(event: ServicingEvent) : LoanServicingAggregate =
        let newState = ServicingAggregate.evolve this.State event
        { this with State = Some newState }

    member this.Execute(command: ServicingCommand) : Result<ServicingEvent list, string> =
        ServicingAggregate.handle this.State command

    static member Rebuild(id: Guid, events: ServicingEvent seq) : LoanServicingAggregate =
        events
        |> Seq.fold (fun (agg: LoanServicingAggregate) e -> agg.Evolve e)
                    (LoanServicingAggregate.Zero id)
