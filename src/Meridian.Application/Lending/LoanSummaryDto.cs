namespace Meridian.Application.Lending;

/// <summary>Flattened read-model projection of a single loan's current state.</summary>
public sealed record LoanSummaryDto(
    Guid LoanId,
    string Name,
    string BaseCurrency,
    string Status,
    DateOnly OriginationDate,
    DateOnly MaturityDate,
    decimal CommitmentAmount,
    decimal OutstandingPrincipal,
    decimal AccruedInterestUnpaid,
    decimal AccruedCommitmentFeeUnpaid,
    decimal UnamortizedDiscount,
    decimal UnamortizedPremium,
    decimal CarryingValue,
    decimal CollateralValue,
    decimal? LoanToValue,
    string? CreditRating,
    bool? IsInvestmentGrade,
    long LastEventSequence,
    DateTimeOffset UpdatedAt
);

/// <summary>Aggregate metrics across the entire loan portfolio.</summary>
public sealed record PortfolioSummaryDto(
    int TotalLoans,
    int ActiveLoans,
    int DistressedLoans,
    decimal TotalCommitmentAmount,
    decimal TotalOutstandingPrincipal,
    decimal TotalCarryingValue,
    decimal TotalCollateralValue,
    decimal TotalAccruedInterestUnpaid
);
