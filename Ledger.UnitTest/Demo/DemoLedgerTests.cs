using Ledger.Library.Enums;
using Ledger.WebApi.Features.Demo.Services;

namespace Ledger.UnitTest.Demo;

/// <summary>
/// The sample ledger is what people see first, and the README's screenshots come from it: its last full month must
/// always add up to the same numbers.
/// </summary>
public sealed class DemoLedgerTests
{
    [Fact]
    public void Generate_OnlyInstalmentsLieAfterToday()
    {
        var today = new DateOnly(2026, 11, 17);
        var demo = DemoData.Generate(today);
        Assert.All(demo.Entries, entry => Assert.True(entry.Day <= today || entry.Plan is not null));
        var laptop = demo.Entries.Where(static entry => entry.Plan is not null).ToList();
        Assert.Equal(6, laptop.Count);
        Assert.Equal(1199.94m, laptop.Sum(static entry => entry.Amount));
        Assert.Equal(new DateOnly(2026, 11, 2), laptop[0].Day);
        Assert.Equal(5, demo.Accounts.Count);
        Assert.Equal([DemoData.SoldProject, DemoData.OpenProject], demo.Projects);
    }

    [Fact]
    public async Task LastFullMonth_AddsUpToTheMockNumbers()
    {
        var harness = new Harness();
        var user = await harness.DemoAsync();

        var october = (await harness.OverviewService.GetAsync(user, "2026-10")).Result!;
        Assert.Equal(2154.90m, october.In);
        Assert.Equal(1327.74m, october.Out);
        Assert.Equal(827.16m, october.Net);
        Assert.Equal(1400m, october.Cap);
        Assert.Equal(6, october.Months.Count);
        Assert.False(october.IsCurrentMonth);
        Assert.Equal("2026-09", october.PreviousMonth);
        Assert.Equal("2026-11", october.NextMonth);
        Assert.Equal(october.Sources.Sum(static part => part.Amount), october.In);
        Assert.Equal(october.Targets.Sum(static part => part.Amount), october.Out);
    }

    [Fact]
    public async Task Rental_LastFullMonth()
    {
        var harness = new Harness();
        var user = await harness.DemoAsync();

        var rental = (await harness.RentalService.GetAsync(user, "2026-10")).Result!;
        Assert.Equal(1665.00m, rental.Revenue);
        Assert.Equal(812.95m, rental.Costs);
        Assert.Equal(852.05m, rental.Margin);
        Assert.Equal(3, rental.BookingCount);
        Assert.Equal(19, rental.NightsBooked);
        Assert.Equal(31, rental.NightsInMonth);
        Assert.Equal([3, 14, 23], rental.Bookings.Where(static booking => booking.PaidThisMonth).Select(static booking => booking.StartDay));
    }

    [Fact]
    public async Task Projects_OneSoldOneInProgress()
    {
        var harness = new Harness();
        var user = await harness.DemoAsync();

        var projects = (await harness.ProjectService.ListAsync(user)).Result!;
        var sold = projects.Projects.Single(static project => project.Name == DemoData.SoldProject);
        Assert.Equal(ProjectState.Sold, sold.State);
        Assert.Equal(117952.20m, sold.Invested);
        Assert.Equal(138000m, sold.Sold);
        Assert.Equal(20047.80m, sold.Result);
        Assert.Equal(0.17m, decimal.Round(sold.Return!.Value, 2));
        Assert.Equal(71, sold.Days);
        Assert.Equal(["deposit", "deed", "works", "market"], sold.Phases.Select(static phase => phase.Key));
        Assert.Equal(sold.Sold, sold.Split.Sum(static part => part.Amount));

        var open = projects.Projects.Single(static project => project.Name == DemoData.OpenProject);
        Assert.Equal(ProjectState.InProgress, open.State);
        Assert.Equal(8400m, open.Invested);
        Assert.Equal(75000m, open.AgreedPrice);

        Assert.Equal(20047.80m, projects.RealisedProfit);
        Assert.Equal(8400m, projects.CapitalAtWork);
        Assert.Equal(1, projects.SoldCount);
        Assert.Equal(1, projects.OpenCount);
    }
}
