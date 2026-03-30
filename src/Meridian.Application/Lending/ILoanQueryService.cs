namespace Meridian.Application.Lending;

/// <summary>
/// Read-side service for the direct-lending domain.
/// Queries the <c>lending.loan_positions</c> read-model table and
/// provides a <c>Project</c> method to refresh that table from the event stream.
/// </summary>
public interface ILoanQueryService
{
    /// <summary>Returns the current projected summary for a loan, or <c>null</c> if not found.</summary>
    LoanSummaryDto? GetSummary(Guid loanId);

    /// <summary>Returns projected summaries for all loans.</summary>
    IReadOnlyList<LoanSummaryDto> GetAll();

    /// <summary>Returns projected summaries for all loans with the specified status string (e.g. "Active").</summary>
    IReadOnlyList<LoanSummaryDto> GetByStatus(string status);

    /// <summary>Returns aggregate metrics across the entire portfolio.</summary>
    PortfolioSummaryDto GetPortfolioSummary();

    /// <summary>
    /// Re-projects a single loan's state from its event stream into <c>lending.loan_positions</c>.
    /// Idempotent; safe to call after every write command.
    /// </summary>
    void ProjectLoan(Guid loanId);

    /// <summary>
    /// Rebuilds the entire <c>lending.loan_positions</c> table by re-projecting every
    /// known loan from its event stream. Use for initial population or catch-up after downtime.
    /// </summary>
    void ProjectAll();
}
