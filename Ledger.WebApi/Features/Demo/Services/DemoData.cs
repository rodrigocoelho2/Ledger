using Ledger.Library.Enums;

namespace Ledger.WebApi.Features.Demo.Services;

/// <summary>One account of the sample ledger.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Kind">Its kind.</param>
/// <param name="OpeningBalance">What it starts with.</param>
internal sealed record DemoAccount(string Name, AccountKind Kind, decimal OpeningBalance);

/// <summary>One entry of the sample ledger, naming its account and project instead of pointing at them.</summary>
/// <param name="Day">The day.</param>
/// <param name="Kind">Which way the money moved.</param>
/// <param name="Scope">The part of life.</param>
/// <param name="Name">What it was.</param>
/// <param name="Category">The category key (null on transfers).</param>
/// <param name="Amount">The amount.</param>
/// <param name="Account">The account's name.</param>
/// <param name="ToAccount">The account a transfer goes to.</param>
/// <param name="Project">The project's name.</param>
/// <param name="Nights">The nights of a booking.</param>
/// <param name="Channel">Where a booking came from.</param>
internal sealed record DemoEntry(
    DateOnly Day,
    EntryKind Kind,
    EntryScope Scope,
    string Name,
    string? Category,
    decimal Amount,
    string Account,
    string? ToAccount = null,
    string? Project = null,
    int? Nights = null,
    string? Channel = null);

/// <summary>The sample ledger of demo mode.</summary>
/// <param name="Accounts">The accounts, in order.</param>
/// <param name="Projects">The names of the buy-and-sell projects.</param>
/// <param name="Caps">The spending caps, by the first day of the month.</param>
/// <param name="Entries">The entries, oldest first.</param>
internal sealed record DemoLedger(
    IReadOnlyList<DemoAccount> Accounts,
    IReadOnlyList<string> Projects,
    IReadOnlyDictionary<DateOnly, decimal> Caps,
    IReadOnlyList<DemoEntry> Entries);

/// <summary>
/// Builds the sample ledger: six months of one person's money ending today, a flat rented by the night for the last
/// four of them, one property bought, renovated and sold, and another just started. Everything is made up, and the
/// same day always gives the same ledger. Nothing is dated after today.
/// </summary>
internal static class DemoData
{
    private static readonly string[] Guests = ["Sophie & Mark", "Lukas", "Ana & Tiago", "The Duponts", "Emma", "Marta & Rui", "Jonas", "Claire & Ben", "Inês", "The Okafors", "Yuki", "Pedro & Sara", "Hannah", "Léa & Hugo", "Tomás"];

    /// <summary>The everyday account.</summary>
    public const string Main = "Main account";

    /// <summary>Savings.</summary>
    public const string Savings = "Savings";

    /// <summary>The broker.</summary>
    public const string Investments = "Investments";

    /// <summary>The meal card.</summary>
    public const string MealCard = "Meal card";

    /// <summary>The account the rental and the projects run through.</summary>
    public const string Property = "Property account";

    /// <summary>The project that is bought, renovated and sold.</summary>
    public const string SoldProject = "T2 Bela Vista";

    /// <summary>The project that has only just started.</summary>
    public const string OpenProject = "T1 Infantado";

    /// <summary>The price agreed for a sample project (the deposit and the deed each pay part of it).</summary>
    /// <param name="project">The project's name.</param>
    /// <returns>The price, or null for a name that isn't a sample project.</returns>
    public static decimal? AgreedPrice(string project) => project switch
    {
        SoldProject => 92000.00m,
        OpenProject => 75000.00m,
        _ => null,
    };

    /// <summary>How many months the sample covers, the one we are in included.</summary>
    public const int MonthCount = 6;

    /// <summary>The cap on personal spending, every month.</summary>
    public const decimal Cap = 1400m;

    // Index 0 is five months ago, 4 is last month, 5 is the month we are in. Zero: nothing that month.
    private static readonly decimal[] Electricity = [61.30m, 57.80m, 64.20m, 58.90m, 71.40m, 69.10m];
    private static readonly decimal[] Supermarket = [128.90m, 141.35m, 132.45m, 118.70m, 147.20m, 139.60m];
    private static readonly decimal[] Grocer = [91.20m, 77.45m, 86.30m, 94.15m, 79.60m, 88.25m];
    private static readonly decimal[] Discounter = [58.10m, 72.30m, 61.90m, 58.40m, 66.75m, 63.20m];
    private static readonly decimal[] DinnerOut = [62.00m, 0m, 48.00m, 72.50m, 55.30m, 44.80m];
    private static readonly decimal[] Fuel = [75.00m, 60.00m, 70.00m, 65.00m, 80.00m, 70.00m];
    private static readonly decimal[] Tolls = [12.40m, 27.60m, 24.30m, 31.80m, 18.90m, 22.10m];
    private static readonly decimal[] Cinema = [0m, 15.00m, 19.40m, 0m, 22.80m, 0m];
    private static readonly decimal[] Pharmacy = [8.90m, 0m, 0m, 34.20m, 12.60m, 0m];
    private static readonly decimal[] Dividends = [0m, 22.40m, 0m, 41.80m, 18.50m, 0m];

    private static readonly (int Day, decimal Amount)[] Lunches =
    [
        (2, 9.40m), (6, 10.20m), (8, 8.90m), (13, 9.80m), (15, 9.60m), (20, 10.50m), (21, 8.70m), (23, 9.90m), (28, 9.50m), (30, 9.80m),
    ];

    /// <summary>
    /// Builds the sample ledger for a day.
    /// </summary>
    /// <param name="today">Today.</param>
    /// <returns>The ledger.</returns>
    public static DemoLedger Generate(DateOnly today)
    {
        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        var entries = new List<DemoEntry>();
        var caps = new Dictionary<DateOnly, decimal>();

        for (var index = 0; index < MonthCount; index++)
        {
            var month = thisMonth.AddMonths(index + 1 - MonthCount);
            caps[month] = Cap;
            Personal(entries, month, index);
        }

        Rental(entries, thisMonth);
        Projects(entries, thisMonth);

        return new DemoLedger(
            [
                new DemoAccount(Main, AccountKind.Current, 1450.00m),
                new DemoAccount(Savings, AccountKind.Savings, 4500.00m),
                new DemoAccount(Investments, AccountKind.Investments, 5200.00m),
                new DemoAccount(MealCard, AccountKind.MealCard, 12.40m),
                new DemoAccount(Property, AccountKind.Current, 118000.00m),
            ],
            [SoldProject, OpenProject],
            caps,
            [.. entries.Where(entry => entry.Day <= today).OrderBy(static entry => entry.Day)]);
    }

    private static void Personal(List<DemoEntry> entries, DateOnly month, int index)
    {
        void Out(int day, string name, string category, decimal amount, string account = Main)
        {
            if (amount > 0)
            {
                entries.Add(new DemoEntry(Day(month, day), EntryKind.Outflow, EntryScope.Personal, name, category, amount, account));
            }
        }

        void In(int day, string name, string category, decimal amount, string account = Main)
        {
            if (amount > 0)
            {
                entries.Add(new DemoEntry(Day(month, day), EntryKind.Inflow, EntryScope.Personal, name, category, amount, account));
            }
        }

        In(1, "Meal allowance", "meal-allowance", 176.40m, MealCard);
        Out(2, "Gym", "gym", 54.90m);
        Out(3, "Continente", "groceries", Supermarket[index]);
        Out(5, "Service charge", "insurance", 38.00m);
        Out(7, "Fuel", "transport", Fuel[index]);
        Out(8, "Mortgage repayment", "loans", 512.40m);
        Out(9, "Pharmacy", "health", Pharmacy[index]);
        Out(11, "Electricity", "bills", Electricity[index]);
        Out(11, "Water", "bills", 21.60m);
        Out(12, "Fibre and mobile", "bills", 49.99m);
        Out(14, "Pingo Doce", "groceries", Grocer[index]);
        Out(16, "Cinema", "leisure", Cinema[index]);
        Out(19, "Via Verde tolls", "transport", Tolls[index]);
        Out(22, "Lidl", "groceries", Discounter[index]);
        In(24, "Salary", "salary", 1960.00m);
        entries.Add(new DemoEntry(Day(month, 25), EntryKind.Transfer, EntryScope.Personal, "Transfer to savings", null, 300.00m, Main, Savings));
        entries.Add(new DemoEntry(Day(month, 25), EntryKind.Transfer, EntryScope.Personal, "Monthly ETF top-up", null, 250.00m, Main, Investments));
        Out(27, "Dinner out", "groceries", DinnerOut[index]);
        In(28, "Dividends VHYL", "dividends", Dividends[index], Investments);

        foreach (var (day, amount) in Lunches)
        {
            Out(day, "Lunch", "groceries", amount, MealCard);
        }

        // The odd thing that only happens once.
        switch (index)
        {
            case 1:
                Out(17, "Weekend in Porto", "leisure", 214.00m);
                break;
            case 2:
                Out(18, "Running shoes", "shopping", 89.90m);
                break;
            case 3:
                Out(20, "Car insurance", "insurance", 286.00m);
                break;
        }
    }

    private static void Rental(List<DemoEntry> entries, DateOnly thisMonth)
    {
        var guest = 0;
        void Booking(DateOnly month, int day, string channel, int nights, decimal amount) =>
            entries.Add(new DemoEntry(Day(month, day), EntryKind.Inflow, EntryScope.Rental, "Stay · " + Guests[guest++ % Guests.Length], "bookings", amount, Property, Nights: nights, Channel: channel));

        void Cost(DateOnly month, int day, string name, string category, decimal amount) =>
            entries.Add(new DemoEntry(Day(month, day), EntryKind.Outflow, EntryScope.Rental, name, category, amount, Property));

        var threeAgo = thisMonth.AddMonths(-3);
        Booking(threeAgo, 4, "Booking", 4, 312.00m);
        Booking(threeAgo, 12, "Airbnb", 3, 258.00m);
        Booking(threeAgo, 21, "Airbnb", 6, 486.00m);
        Cost(threeAgo, 5, "Cleaning after check-out", "cleaning", 135.00m);
        Cost(threeAgo, 6, "Coffee, toiletries and paper", "guest-supplies", 42.60m);
        Cost(threeAgo, 11, "Electricity and water", "bills", 78.40m);
        Cost(threeAgo, 15, "Service charge and insurance", "insurance", 64.00m);
        Cost(threeAgo, 18, "Cleaning after check-out", "cleaning", 90.00m);
        Cost(threeAgo, 20, "Small repairs", "maintenance", 45.00m);
        Cost(threeAgo, 28, "Booking and Airbnb fees", "platform-fees", 157.80m);

        var twoAgo = thisMonth.AddMonths(-2);
        Booking(twoAgo, 2, "Airbnb", 5, 430.00m);
        Booking(twoAgo, 10, "Booking", 7, 602.00m);
        Booking(twoAgo, 19, "Direct", 4, 344.00m);
        Booking(twoAgo, 27, "Booking", 3, 249.00m);
        Cost(twoAgo, 5, "Cleaning after check-out", "cleaning", 180.00m);
        Cost(twoAgo, 6, "Coffee, toiletries and paper", "guest-supplies", 58.20m);
        Cost(twoAgo, 11, "Electricity and water", "bills", 92.10m);
        Cost(twoAgo, 15, "Service charge and insurance", "insurance", 64.00m);
        Cost(twoAgo, 18, "Cleaning after check-out", "cleaning", 120.00m);
        Cost(twoAgo, 20, "New bed linen", "maintenance", 210.00m);
        Cost(twoAgo, 28, "Booking and Airbnb fees", "platform-fees", 238.35m);
        Cost(twoAgo, 30, "Property tax, second instalment", "taxes", 187.40m);

        var lastMonth = thisMonth.AddMonths(-1);
        Booking(lastMonth, 3, "Airbnb", 8, 712.00m);
        Booking(lastMonth, 14, "Booking", 5, 455.00m);
        Booking(lastMonth, 23, "Direct", 6, 498.00m);
        Cost(lastMonth, 6, "Coffee, toiletries and paper", "guest-supplies", 51.30m);
        Cost(lastMonth, 11, "Cleaning after check-out", "cleaning", 165.00m);
        Cost(lastMonth, 11, "Electricity and water", "bills", 104.60m);
        Cost(lastMonth, 15, "Service charge and insurance", "insurance", 64.00m);
        Cost(lastMonth, 19, "Cleaning after check-out", "cleaning", 110.00m);
        Cost(lastMonth, 20, "Small repairs", "maintenance", 68.00m);
        Cost(lastMonth, 28, "Booking and Airbnb fees", "platform-fees", 250.05m);

        Booking(thisMonth, 4, "Airbnb", 6, 534.00m);
        Booking(thisMonth, 12, "Booking", 4, 368.00m);
        Booking(thisMonth, 21, "Direct", 5, 445.00m);
        Cost(thisMonth, 6, "Coffee, toiletries and paper", "guest-supplies", 47.80m);
        Cost(thisMonth, 10, "Cleaning after check-out", "cleaning", 110.00m);
        Cost(thisMonth, 11, "Electricity and water", "bills", 96.30m);
        Cost(thisMonth, 15, "Service charge and insurance", "insurance", 64.00m);
        Cost(thisMonth, 16, "Cleaning after check-out", "cleaning", 90.00m);
        Cost(thisMonth, 28, "Booking and Airbnb fees", "platform-fees", 201.90m);
    }

    private static void Projects(List<DemoEntry> entries, DateOnly thisMonth)
    {
        void Cost(DateOnly month, int day, string name, string category, decimal amount, string project) =>
            entries.Add(new DemoEntry(Day(month, day), EntryKind.Outflow, EntryScope.Flips, name, category, amount, Property, Project: project));

        var threeAgo = thisMonth.AddMonths(-3);
        var twoAgo = thisMonth.AddMonths(-2);
        var lastMonth = thisMonth.AddMonths(-1);

        Cost(threeAgo, 2, "Deposit for the T2 on Rua da Bela Vista", "purchase", 9000.00m, SoldProject);
        Cost(threeAgo, 18, "Deed: the rest of the price", "purchase", 83000.00m, SoldProject);
        Cost(threeAgo, 18, "Transfer tax, stamp duty and notary", "deed", 4120.00m, SoldProject);
        Cost(threeAgo, 26, "Demolition and rubble removal", "works", 1850.00m, SoldProject);
        Cost(twoAgo, 3, "Plumbing and electrics", "labour", 3400.00m, SoldProject);
        Cost(twoAgo, 9, "Sanitary ware, taps and tiles", "works", 2780.00m, SoldProject);
        Cost(twoAgo, 15, "Laminate floor and skirting", "works", 1960.00m, SoldProject);
        Cost(twoAgo, 21, "Painting", "labour", 1450.00m, SoldProject);
        Cost(twoAgo, 24, "Fitted kitchen", "works", 4300.00m, SoldProject);
        Cost(twoAgo, 28, "Energy certificate and permits", "permits", 380.00m, SoldProject);
        Cost(lastMonth, 4, "Home staging and photos", "works", 620.00m, SoldProject);
        entries.Add(new DemoEntry(Day(lastMonth, 12), EntryKind.Inflow, EntryScope.Flips, "Sale of the T2 on Rua da Bela Vista", "sale", 138000.00m, Property, Project: SoldProject));
        Cost(lastMonth, 12, "Estate agency, 3% plus VAT", "agency", 5092.20m, SoldProject);

        Cost(lastMonth, 20, "Deposit for the T1 in Infantado", "purchase", 7500.00m, OpenProject);
        Cost(lastMonth, 26, "Architecture project", "permits", 900.00m, OpenProject);
    }

    private static DateOnly Day(DateOnly month, int day) => new(month.Year, month.Month, Math.Min(day, DateTime.DaysInMonth(month.Year, month.Month)));
}
