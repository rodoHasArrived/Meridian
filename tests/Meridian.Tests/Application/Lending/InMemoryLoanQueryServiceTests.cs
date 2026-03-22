using FluentAssertions;
using Meridian.Application.Lending;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FSharp.Core;
using Xunit;
using FSharpLending = Meridian.FSharp.Domain.Lending;
using InMemoryStore = Meridian.Lending.EventStore.InMemoryLoanEventStore;

namespace Meridian.Tests.Application.Lending;

/// <summary>
/// Tests for <see cref="InMemoryLoanQueryService"/>.
/// Verifies on-the-fly projection, filtering, and portfolio aggregation.
/// </summary>
public sealed class InMemoryLoanQueryServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (InMemoryLendingService lending, InMemoryLoanQueryService query) CreateServices()
    {
        var lending = new InMemoryLendingService(NullLogger<InMemoryLendingService>.Instance);
        var store = new InMemoryStore.InMemoryLoanEventStore();
        var query = new InMemoryLoanQueryService(lending, store);
        return (lending, query);
    }

    private static FSharpLending.LoanHeader MakeHeader(Guid id) =>
        new(id, "Test Loan", FSharpLending.Currency.USD, new DateOnly(2025, 1, 15));

    private static FSharpLending.DirectLendingTerms MakeTerms(decimal commitment = 1_000_000m) => new(
        new DateOnly(2025, 1, 15),
        new DateOnly(2028, 1, 15),
        commitment,
        FSharpOption<decimal>.None,
        FSharpOption<decimal>.Some(0.08m),
        FSharpOption<string>.None,
        FSharpOption<decimal>.None,
        3,
        FSharpLending.AmortizationType.BulletMaturity,
        FSharpLending.DayCountConvention.Actual360,
        FSharpOption<decimal>.None,
        FSharpOption<string>.None);

    private static Guid CreateLoan(InMemoryLendingService svc, decimal commitment = 1_000_000m)
    {
        var id = Guid.NewGuid();
        svc.Handle(id, FSharpLending.LoanCommand.NewCreateLoan(MakeHeader(id), MakeTerms(commitment)))
           .IsSuccess.Should().BeTrue();
        return id;
    }

    private static void ActivateLoan(InMemoryLendingService svc, Guid id, decimal amount = 1_000_000m)
    {
        svc.Handle(id, FSharpLending.LoanCommand.NewCommitLoan(amount, FSharpLending.Currency.USD))
           .IsSuccess.Should().BeTrue();
        svc.Handle(id, FSharpLending.LoanCommand.NewRecordDrawdown(amount, FSharpLending.Currency.USD, new DateOnly(2025, 1, 15)))
           .IsSuccess.Should().BeTrue();
    }

    // ── GetSummary ────────────────────────────────────────────────────────────

    [Fact]
    public void GetSummary_ReturnsNull_WhenLoanDoesNotExist()
    {
        var (_, query) = CreateServices();
        query.GetSummary(Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void GetSummary_ReturnsDto_AfterLoanCreated()
    {
        var (lending, query) = CreateServices();
        var id = CreateLoan(lending);

        var dto = query.GetSummary(id);

        dto.Should().NotBeNull();
        dto!.LoanId.Should().Be(id);
        dto.Status.Should().Be("Pending");
        dto.BaseCurrency.Should().Be("USD");
        dto.CommitmentAmount.Should().Be(1_000_000m);
    }

    [Fact]
    public void GetSummary_ReflectsActiveStatus_AfterDrawdown()
    {
        var (lending, query) = CreateServices();
        var id = CreateLoan(lending);
        ActivateLoan(lending, id);

        var dto = query.GetSummary(id);

        dto.Should().NotBeNull();
        dto!.Status.Should().Be("Active");
        dto.OutstandingPrincipal.Should().Be(1_000_000m);
    }

    [Fact]
    public void GetSummary_ReflectsClosedStatus_AfterFullLifecycle()
    {
        var (lending, query) = CreateServices();
        var id = CreateLoan(lending);
        ActivateLoan(lending, id);
        lending.Handle(id, FSharpLending.LoanCommand.NewRepayPrincipal(1_000_000m, new DateOnly(2028, 1, 15)));
        lending.Handle(id, FSharpLending.LoanCommand.NewCloseLoan(new DateOnly(2028, 1, 15)));

        var dto = query.GetSummary(id);

        dto!.Status.Should().Be("Closed");
        dto.OutstandingPrincipal.Should().Be(0m);
    }

    [Fact]
    public void GetSummary_HasNullCreditRating_WhenNotAssigned()
    {
        var (lending, query) = CreateServices();
        var id = CreateLoan(lending);

        var dto = query.GetSummary(id);

        dto!.CreditRating.Should().BeNull();
        dto.IsInvestmentGrade.Should().BeNull();
    }

    // ── GetAll ────────────────────────────────────────────────────────────────

    [Fact]
    public void GetAll_ReturnsEmpty_WhenNoLoans()
    {
        var (_, query) = CreateServices();
        query.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void GetAll_ReturnsAllCreatedLoans()
    {
        var (lending, query) = CreateServices();
        var id1 = CreateLoan(lending);
        var id2 = CreateLoan(lending);

        var all = query.GetAll();

        all.Should().HaveCount(2);
        all.Select(d => d.LoanId).Should().Contain(id1).And.Contain(id2);
    }

    // ── GetByStatus ───────────────────────────────────────────────────────────

    [Fact]
    public void GetByStatus_ReturnsOnlyMatchingLoans()
    {
        var (lending, query) = CreateServices();
        var id1 = CreateLoan(lending);
        var id2 = CreateLoan(lending);
        ActivateLoan(lending, id2);

        var pending = query.GetByStatus("Pending");
        var active  = query.GetByStatus("Active");

        pending.Should().ContainSingle(d => d.LoanId == id1);
        active.Should().ContainSingle(d => d.LoanId == id2);
    }

    [Fact]
    public void GetByStatus_ReturnsEmpty_WhenNoMatchingLoans()
    {
        var (lending, query) = CreateServices();
        CreateLoan(lending);

        query.GetByStatus("Closed").Should().BeEmpty();
    }

    // ── GetPortfolioSummary ───────────────────────────────────────────────────

    [Fact]
    public void GetPortfolioSummary_ReturnsZeros_WhenNoLoans()
    {
        var (_, query) = CreateServices();
        var summary = query.GetPortfolioSummary();

        summary.TotalLoans.Should().Be(0);
        summary.TotalCommitmentAmount.Should().Be(0m);
    }

    [Fact]
    public void GetPortfolioSummary_CountsLoansCorrectly()
    {
        var (lending, query) = CreateServices();
        var id1 = CreateLoan(lending, 2_000_000m);
        var id2 = CreateLoan(lending, 3_000_000m);
        ActivateLoan(lending, id1, 2_000_000m);

        var summary = query.GetPortfolioSummary();

        summary.TotalLoans.Should().Be(2);
        summary.ActiveLoans.Should().Be(1);
        summary.DistressedLoans.Should().Be(0);
        summary.TotalCommitmentAmount.Should().Be(5_000_000m);
        summary.TotalOutstandingPrincipal.Should().Be(2_000_000m);
    }

    // ── ProjectLoan / ProjectAll (no-ops) ─────────────────────────────────────

    [Fact]
    public void ProjectLoan_IsNoOp_DoesNotThrow()
    {
        var (_, query) = CreateServices();
        var act = () => query.ProjectLoan(Guid.NewGuid());
        act.Should().NotThrow();
    }

    [Fact]
    public void ProjectAll_IsNoOp_DoesNotThrow()
    {
        var (_, query) = CreateServices();
        var act = () => query.ProjectAll();
        act.Should().NotThrow();
    }
}
