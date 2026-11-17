using Ledger.Library.Enums;
using Ledger.WebApi.Features.Accounts.Services;
using Ledger.WebApi.Features.Overview.Services;

namespace Ledger.UnitTest.Overview;

public sealed class OverviewMathTests
{
    private static readonly DateOnly Day = new(2026, 10, 10);

    [Fact]
    public void Totals_LeaveTransfersOut()
    {
        var entries = new[]
        {
            Make.Entry(EntryKind.Inflow, 1850m, Day, "salary"),
            Make.Entry(EntryKind.Outflow, 650m, Day, "home"),
            Make.Entry(EntryKind.Outflow, 9.80m, Day, "groceries"),
            Make.Entry(EntryKind.Transfer, 300m, Day),
        };

        var (incoming, outgoing) = OverviewMath.Totals(entries);
        Assert.Equal(1850m, incoming);
        Assert.Equal(659.80m, outgoing);
    }

    [Fact]
    public void Parts_AreBiggestFirstWithTheirShare()
    {
        var entries = new[]
        {
            Make.Entry(EntryKind.Outflow, 25m, Day, "gym"),
            Make.Entry(EntryKind.Outflow, 50m, Day, "groceries"),
            Make.Entry(EntryKind.Outflow, 25m, Day, "groceries"),
            Make.Entry(EntryKind.Inflow, 1000m, Day, "salary"),
        };

        var parts = OverviewMath.Parts(entries, EntryKind.Outflow);
        Assert.Equal(["groceries", "gym"], parts.Select(static part => part.Key));
        Assert.Equal(75m, parts[0].Amount);
        Assert.Equal(0.75m, parts[0].Share);
    }

    [Theory]
    [InlineData(2000, 1500, 0.25)]
    [InlineData(1000, 1000, 0)]
    [InlineData(1000, 1250, -0.25)]
    public void SavingsRate_IsWhatWasKeptOfWhatCameIn(double incoming, double outgoing, double expected) =>
        Assert.Equal((decimal)expected, OverviewMath.SavingsRate((decimal)incoming, (decimal)outgoing));

    [Fact]
    public void SavingsRate_IsNothingWithoutIncome() => Assert.Null(OverviewMath.SavingsRate(0m, 120m));

    [Fact]
    public void Strip_IsSixMonthsEndingAtTheCurrentOne()
    {
        var current = new DateOnly(2026, 11, 1);
        var strip = OverviewMath.Strip(new DateOnly(2026, 10, 1), current);
        Assert.Equal(6, strip.Count);
        Assert.Equal(new DateOnly(2026, 6, 1), strip[0]);
        Assert.Equal(current, strip[^1]);
        Assert.Contains(new DateOnly(2026, 10, 1), strip);
    }

    [Fact]
    public void Strip_FollowsAMonthLongAgo()
    {
        var selected = new DateOnly(2025, 1, 1);
        var strip = OverviewMath.Strip(selected, new DateOnly(2026, 11, 1));
        Assert.Equal(6, strip.Count);
        Assert.Contains(selected, strip);
    }

    [Fact]
    public void Movement_CountsInOutAndBothSidesOfATransfer()
    {
        var main = Guid.NewGuid();
        var savings = Guid.NewGuid();
        var entries = new[]
        {
            Make.Entry(EntryKind.Inflow, 1000m, Day, "salary", account: main),
            Make.Entry(EntryKind.Outflow, 100m, Day, "groceries", account: main),
            Make.Entry(EntryKind.Transfer, 300m, Day, account: main, to: savings),
            Make.Entry(EntryKind.Outflow, 5m, Day, "groceries", account: savings),
        };

        Assert.Equal(600m, Balances.Movement(main, entries));
        Assert.Equal(295m, Balances.Movement(savings, entries));
    }
}
