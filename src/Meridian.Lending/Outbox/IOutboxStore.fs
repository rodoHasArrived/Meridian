/// Abstraction over outbox storage for the Lending domain.
/// Enables reliable at-least-once delivery of domain events to downstream consumers.
module Meridian.Lending.Outbox.IOutboxStore

open System

/// Status of an outbox entry.
[<RequireQualifiedAccess>]
type OutboxStatus =
    | Pending
    | Processed
    | DeadLettered

/// A single outbox entry.
[<CLIMutable>]
type OutboxEntry = {
    Id: int64
    AggregateId: Guid
    /// "LoanContract" | "LoanServicing"
    AggregateType: string
    /// Event type name (e.g. "DrawdownExecuted").
    EventType: string
    PayloadJson: string
    CorrelationId: Guid option
    CausationId: Guid option
    CreatedAt: DateTimeOffset
    ProcessedAt: DateTimeOffset option
    RetryCount: int
    DeadLetteredAt: DateTimeOffset option
    LastError: string option
}

/// New entry to append to the outbox — a subset of OutboxEntry without DB-assigned fields.
type NewOutboxEntry = {
    AggregateId: Guid
    AggregateType: string
    EventType: string
    PayloadJson: string
    CorrelationId: Guid option
    CausationId: Guid option
}

/// Abstraction over the outbox store.
type IOutboxStore =
    /// Appends a batch of entries to the outbox (typically called inside the same DB transaction
    /// that appends the domain events, to guarantee atomicity).
    abstract Append: entries: NewOutboxEntry list -> unit

    /// Returns up to <paramref name="batchSize"/> unprocessed entries in creation order.
    abstract GetPending: batchSize: int -> OutboxEntry list

    /// Marks an entry as successfully processed.
    abstract MarkProcessed: id: int64 -> unit

    /// Records a failed dispatch attempt; increments retry_count and stores the error.
    /// When <paramref name="deadLetter"/> is true the entry is moved to the dead-letter state.
    abstract RecordFailure: id: int64 * error: string * deadLetter: bool -> unit
