using Microsoft.Extensions.Logging;
using Npgsql;
using LoanProjector = Meridian.Lending.Projections.PostgresLoanPositionProjector;
using LendingStore = Meridian.Lending.EventStore.ILoanEventStore;

namespace Meridian.Application.Lending;

/// <summary>
/// PostgreSQL-backed implementation of <see cref="ILoanQueryService"/>.
/// Reads from the <c>lending.loan_positions</c> read-model table and delegates
/// projection writes to the F# <c>PostgresLoanPositionProjector</c>.
/// </summary>
public sealed class PostgresLoanQueryService : ILoanQueryService
{
    private readonly LendingStore.ILoanEventStore _store;
    private readonly string _connectionString;
    private readonly ILogger<PostgresLoanQueryService> _logger;

    public PostgresLoanQueryService(
        LendingStore.ILoanEventStore store,
        string connectionString,
        ILogger<PostgresLoanQueryService> logger)
    {
        _store = store;
        _connectionString = connectionString;
        _logger = logger;
    }

    /// <inheritdoc/>
    public LoanSummaryDto? GetSummary(Guid loanId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM lending.loan_positions WHERE loan_id = @id";
        cmd.Parameters.AddWithValue("id", loanId);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapRow(reader) : null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<LoanSummaryDto> GetAll()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM lending.loan_positions ORDER BY name";
        using var reader = cmd.ExecuteReader();
        var results = new List<LoanSummaryDto>();
        while (reader.Read())
            results.Add(MapRow(reader));
        return results;
    }

    /// <inheritdoc/>
    public IReadOnlyList<LoanSummaryDto> GetByStatus(string status)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM lending.loan_positions WHERE status = @status ORDER BY name";
        cmd.Parameters.AddWithValue("status", status);
        using var reader = cmd.ExecuteReader();
        var results = new List<LoanSummaryDto>();
        while (reader.Read())
            results.Add(MapRow(reader));
        return results;
    }

    /// <inheritdoc/>
    public PortfolioSummaryDto GetPortfolioSummary()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                COUNT(*)                                                          AS total_loans,
                COUNT(*) FILTER (WHERE status = 'Active')                         AS active_loans,
                COUNT(*) FILTER (WHERE status IN ('NonPerforming','Default','Workout')) AS distressed_loans,
                COALESCE(SUM(commitment_amount),       0)                         AS total_commitment_amount,
                COALESCE(SUM(outstanding_principal),   0)                         AS total_outstanding_principal,
                COALESCE(SUM(carrying_value),          0)                         AS total_carrying_value,
                COALESCE(SUM(collateral_value),        0)                         AS total_collateral_value,
                COALESCE(SUM(accrued_interest_unpaid), 0)                         AS total_accrued_interest_unpaid
            FROM lending.loan_positions
            """;
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return new PortfolioSummaryDto(0, 0, 0, 0m, 0m, 0m, 0m, 0m);

        return new PortfolioSummaryDto(
            TotalLoans:                (int)reader.GetInt64(reader.GetOrdinal("total_loans")),
            ActiveLoans:               (int)reader.GetInt64(reader.GetOrdinal("active_loans")),
            DistressedLoans:           (int)reader.GetInt64(reader.GetOrdinal("distressed_loans")),
            TotalCommitmentAmount:     reader.GetDecimal(reader.GetOrdinal("total_commitment_amount")),
            TotalOutstandingPrincipal: reader.GetDecimal(reader.GetOrdinal("total_outstanding_principal")),
            TotalCarryingValue:        reader.GetDecimal(reader.GetOrdinal("total_carrying_value")),
            TotalCollateralValue:      reader.GetDecimal(reader.GetOrdinal("total_collateral_value")),
            TotalAccruedInterestUnpaid: reader.GetDecimal(reader.GetOrdinal("total_accrued_interest_unpaid"))
        );
    }

    /// <inheritdoc/>
    public void ProjectLoan(Guid loanId)
    {
        _logger.LogInformation("Projecting loan {LoanId} into read model", loanId);
        LoanProjector.projectLoan(_store, _connectionString, loanId);
    }

    /// <inheritdoc/>
    public void ProjectAll()
    {
        _logger.LogInformation("Rebuilding all loan positions in read model");
        LoanProjector.projectAll(_store, _connectionString);
    }

    private static LoanSummaryDto MapRow(NpgsqlDataReader r)
    {
        int ltvOrd  = r.GetOrdinal("loan_to_value");
        int crOrd   = r.GetOrdinal("credit_rating");
        int igOrd   = r.GetOrdinal("is_investment_grade");

        return new LoanSummaryDto(
            LoanId:                    r.GetGuid(r.GetOrdinal("loan_id")),
            Name:                      r.GetString(r.GetOrdinal("name")),
            BaseCurrency:              r.GetString(r.GetOrdinal("base_currency")),
            Status:                    r.GetString(r.GetOrdinal("status")),
            OriginationDate:           r.GetFieldValue<DateOnly>(r.GetOrdinal("origination_date")),
            MaturityDate:              r.GetFieldValue<DateOnly>(r.GetOrdinal("maturity_date")),
            CommitmentAmount:          r.GetDecimal(r.GetOrdinal("commitment_amount")),
            OutstandingPrincipal:      r.GetDecimal(r.GetOrdinal("outstanding_principal")),
            AccruedInterestUnpaid:     r.GetDecimal(r.GetOrdinal("accrued_interest_unpaid")),
            AccruedCommitmentFeeUnpaid: r.GetDecimal(r.GetOrdinal("accrued_commitment_fee_unpaid")),
            UnamortizedDiscount:       r.GetDecimal(r.GetOrdinal("unamortized_discount")),
            UnamortizedPremium:        r.GetDecimal(r.GetOrdinal("unamortized_premium")),
            CarryingValue:             r.GetDecimal(r.GetOrdinal("carrying_value")),
            CollateralValue:           r.GetDecimal(r.GetOrdinal("collateral_value")),
            LoanToValue:               r.IsDBNull(ltvOrd) ? null : r.GetDecimal(ltvOrd),
            CreditRating:              r.IsDBNull(crOrd)  ? null : r.GetString(crOrd),
            IsInvestmentGrade:         r.IsDBNull(igOrd)  ? null : r.GetBoolean(igOrd),
            LastEventSequence:         r.GetInt64(r.GetOrdinal("last_event_sequence")),
            UpdatedAt:                 r.GetFieldValue<DateTimeOffset>(r.GetOrdinal("updated_at"))
        );
    }
}
