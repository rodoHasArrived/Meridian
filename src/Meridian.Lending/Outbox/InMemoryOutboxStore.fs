/// In-memory implementation of IOutboxStore for development and tests.
module Meridian.Lending.Outbox.InMemoryOutboxStore

open System
open System.Collections.Generic
open Meridian.Lending.Outbox.IOutboxStore

/// Thread-safe in-memory outbox store.
type InMemoryOutboxStore() =
    let lockObj = obj ()
    let entries = Dictionary<int64, OutboxEntry>()
    let mutable nextId = 1L

    interface IOutboxStore with
        member _.Append(newEntries: NewOutboxEntry list) =
            lock lockObj (fun () ->
                for e in newEntries do
                    let entry = {
                        Id             = nextId
                        AggregateId    = e.AggregateId
                        AggregateType  = e.AggregateType
                        EventType      = e.EventType
                        PayloadJson    = e.PayloadJson
                        CorrelationId  = e.CorrelationId
                        CausationId    = e.CausationId
                        CreatedAt      = DateTimeOffset.UtcNow
                        ProcessedAt    = None
                        RetryCount     = 0
                        DeadLetteredAt = None
                        LastError      = None
                    }
                    entries.[nextId] <- entry
                    nextId <- nextId + 1L)

        member _.GetPending(batchSize: int) =
            lock lockObj (fun () ->
                entries.Values
                |> Seq.filter (fun e -> e.ProcessedAt.IsNone && e.DeadLetteredAt.IsNone)
                |> Seq.sortBy (fun e -> e.CreatedAt)
                |> Seq.truncate batchSize
                |> Seq.toList)

        member _.MarkProcessed(id: int64) =
            lock lockObj (fun () ->
                match entries.TryGetValue(id) with
                | true, e -> entries.[id] <- { e with ProcessedAt = Some DateTimeOffset.UtcNow }
                | _ -> ())

        member _.RecordFailure(id: int64, error: string, deadLetter: bool) =
            lock lockObj (fun () ->
                match entries.TryGetValue(id) with
                | true, e ->
                    entries.[id] <- {
                        e with
                            RetryCount     = e.RetryCount + 1
                            LastError      = Some error
                            DeadLetteredAt = if deadLetter then Some DateTimeOffset.UtcNow else e.DeadLetteredAt
                    }
                | _ -> ())
