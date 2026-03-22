using Microsoft.FSharp.Core;
using FSharpLending = Meridian.FSharp.Domain.Lending;
using LoanProjection = Meridian.Lending.Projections.LoanProjection;
using LendingStore = Meridian.Lending.EventStore.ILoanEventStore;

namespace Meridian.Application.Lending;

/// <summary>
/// In-memory implementation of <see cref="ILoanQueryService"/> that projects loan state
/// on the fly from an <see cref="ILendingService"/> — no database required.
/// Suitable for unit tests and single-process development use.
/// </summary>
public sealed class InMemoryLoanQueryService : ILoanQueryService
{
    private readonly ILendingService _lendingService;

    public InMemoryLoanQueryService(
        ILendingService lendingService,
        LendingStore.ILoanEventStore store)
    {
        _lendingService = lendingService;
        // store is accepted for API/DI symmetry; in-memory projection is on-the-fly.
        _ = store;
    }

    /// <inheritdoc/>
    public LoanSummaryDto? GetSummary(Guid loanId)
    {
        var state = _lendingService.GetState(loanId);
        return state is null ? null : ToDto(loanId, state);
    }

    /// <inheritdoc/>
    public IReadOnlyList<LoanSummaryDto> GetAll() =>
        _lendingService.GetAllLoanIds()
            .Select(id => GetSummary(id))
            .Where(dto => dto is not null)
            .Cast<LoanSummaryDto>()
            .ToList();

    /// <inheritdoc/>
    public IReadOnlyList<LoanSummaryDto> GetByStatus(string status) =>
        GetAll().Where(dto => dto.Status == status).ToList();

    /// <inheritdoc/>
    public PortfolioSummaryDto GetPortfolioSummary()
    {
        var all = GetAll();
        return new PortfolioSummaryDto(
            TotalLoans:                 all.Count,
            ActiveLoans:                all.Count(d => d.Status == "Active"),
            DistressedLoans:            all.Count(d => d.Status is "NonPerforming" or "Default" or "Workout"),
            TotalCommitmentAmount:      all.Sum(d => d.CommitmentAmount),
            TotalOutstandingPrincipal:  all.Sum(d => d.OutstandingPrincipal),
            TotalCarryingValue:         all.Sum(d => d.CarryingValue),
            TotalCollateralValue:       all.Sum(d => d.CollateralValue),
            TotalAccruedInterestUnpaid: all.Sum(d => d.AccruedInterestUnpaid)
        );
    }

    /// <inheritdoc/>
    /// <remarks>No-op for the in-memory implementation; projection is always on-the-fly.</remarks>
    public void ProjectLoan(Guid loanId) { }

    /// <inheritdoc/>
    /// <remarks>No-op for the in-memory implementation; projection is always on-the-fly.</remarks>
    public void ProjectAll() { }

    private static LoanSummaryDto ToDto(Guid loanId, FSharpLending.LoanState state)
    {
        var row = LoanProjection.project(loanId, state, 0L);
        return new LoanSummaryDto(
            LoanId:                    row.LoanId,
            Name:                      row.Name,
            BaseCurrency:              row.BaseCurrency,
            Status:                    row.Status,
            OriginationDate:           row.OriginationDate,
            MaturityDate:              row.MaturityDate,
            CommitmentAmount:          row.CommitmentAmount,
            OutstandingPrincipal:      row.OutstandingPrincipal,
            AccruedInterestUnpaid:     row.AccruedInterestUnpaid,
            AccruedCommitmentFeeUnpaid: row.AccruedCommitmentFeeUnpaid,
            UnamortizedDiscount:       row.UnamortizedDiscount,
            UnamortizedPremium:        row.UnamortizedPremium,
            CarryingValue:             row.CarryingValue,
            CollateralValue:           row.CollateralValue,
            LoanToValue:               UnwrapOption(row.LoanToValue),
            CreditRating:              UnwrapRefOption(row.CreditRating),
            IsInvestmentGrade:         UnwrapOption(row.IsInvestmentGrade),
            LastEventSequence:         row.LastEventSequence,
            UpdatedAt:                 row.UpdatedAt
        );
    }

    private static T? UnwrapOption<T>(FSharpOption<T> opt) where T : struct =>
        FSharpOption<T>.get_IsSome(opt) ? opt.Value : null;

    private static T? UnwrapRefOption<T>(FSharpOption<T> opt) where T : class =>
        FSharpOption<T>.get_IsSome(opt) ? opt.Value : null;
}
