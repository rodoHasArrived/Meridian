/// PostgreSQL-backed implementation of IOutboxStore.
/// Requires the table created by deploy/sql/lending/V4__outbox.sql.
module Meridian.Lending.Outbox.PostgresOutboxStore

open System
open Npgsql
open IOutboxStore

type PostgresOutboxStore(connectionString: string) =
    let openConnection () =
        let conn = new NpgsqlConnection(connectionString)
        conn.Open()
        conn

    interface IOutboxStore with

        member _.Append(newEntries: NewOutboxEntry list) =
            if newEntries.IsEmpty then ()
            else
                use conn = openConnection()
                use tx = conn.BeginTransaction()
                for e in newEntries do
                    use cmd = conn.CreateCommand()
                    cmd.Transaction <- tx
                    cmd.CommandText <- """
                        INSERT INTO lending.outbox
                            (aggregate_id, aggregate_type, event_type, payload_json,
                             correlation_id, causation_id)
                        VALUES
                            (@aggregateId, @aggregateType, @eventType, @payloadJson,
                             @correlationId, @causationId)
                    """
                    cmd.Parameters.AddWithValue("aggregateId",   e.AggregateId)   |> ignore
                    cmd.Parameters.AddWithValue("aggregateType", e.AggregateType) |> ignore
                    cmd.Parameters.AddWithValue("eventType",     e.EventType)     |> ignore
                    cmd.Parameters.AddWithValue("payloadJson",   e.PayloadJson)   |> ignore
                    cmd.Parameters.AddWithValue("correlationId",
                        match e.CorrelationId with Some g -> box g | None -> box DBNull.Value) |> ignore
                    cmd.Parameters.AddWithValue("causationId",
                        match e.CausationId with Some g -> box g | None -> box DBNull.Value) |> ignore
                    cmd.ExecuteNonQuery() |> ignore
                tx.Commit()

        member _.GetPending(batchSize: int) =
            use conn = openConnection()
            use cmd = conn.CreateCommand()
            cmd.CommandText <- """
                SELECT id, aggregate_id, aggregate_type, event_type, payload_json,
                       correlation_id, causation_id, created_at, processed_at,
                       retry_count, dead_lettered_at, last_error
                FROM lending.outbox
                WHERE processed_at IS NULL AND dead_lettered_at IS NULL
                ORDER BY created_at ASC
                LIMIT @batchSize
            """
            cmd.Parameters.AddWithValue("batchSize", batchSize) |> ignore
            use reader = cmd.ExecuteReader()
            let results = ResizeArray()
            while reader.Read() do
                let nullableGuid (ordinal: int) =
                    if reader.IsDBNull(ordinal) then None
                    else Some (reader.GetGuid(ordinal))
                let nullableDto (ordinal: int) =
                    if reader.IsDBNull(ordinal) then None
                    else Some (reader.GetDateTime(ordinal) |> DateTimeOffset)
                let nullableStr (ordinal: int) =
                    if reader.IsDBNull(ordinal) then None
                    else Some (reader.GetString(ordinal))
                results.Add({
                    Id             = reader.GetInt64(0)
                    AggregateId    = reader.GetGuid(1)
                    AggregateType  = reader.GetString(2)
                    EventType      = reader.GetString(3)
                    PayloadJson    = reader.GetString(4)
                    CorrelationId  = nullableGuid 5
                    CausationId    = nullableGuid 6
                    CreatedAt      = DateTimeOffset(reader.GetDateTime(7))
                    ProcessedAt    = nullableDto 8
                    RetryCount     = reader.GetInt16(9) |> int
                    DeadLetteredAt = nullableDto 10
                    LastError      = nullableStr 11
                })
            results |> Seq.toList

        member _.MarkProcessed(id: int64) =
            use conn = openConnection()
            use cmd = conn.CreateCommand()
            cmd.CommandText <- """
                UPDATE lending.outbox
                SET processed_at = NOW()
                WHERE id = @id
            """
            cmd.Parameters.AddWithValue("id", id) |> ignore
            cmd.ExecuteNonQuery() |> ignore

        member _.RecordFailure(id: int64, error: string, deadLetter: bool) =
            use conn = openConnection()
            use cmd = conn.CreateCommand()
            cmd.CommandText <- """
                UPDATE lending.outbox
                SET retry_count     = retry_count + 1,
                    last_error      = @error,
                    dead_lettered_at = CASE WHEN @deadLetter THEN NOW() ELSE dead_lettered_at END
                WHERE id = @id
            """
            cmd.Parameters.AddWithValue("id",         id)         |> ignore
            cmd.Parameters.AddWithValue("error",      error)      |> ignore
            cmd.Parameters.AddWithValue("deadLetter", deadLetter) |> ignore
            cmd.ExecuteNonQuery() |> ignore
