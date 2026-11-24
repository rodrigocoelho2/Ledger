namespace Ledger.Library.Enums;

/// <summary>
/// The role of a user.
/// </summary>
public enum UserRole
{
    /// <summary>A regular user: their own ledger.</summary>
    User,

    /// <summary>The first user: can also delete other users.</summary>
    Admin,
}

/// <summary>
/// Which way an entry moves money. The numbers are stored in the database: never renumber them.
/// </summary>
public enum EntryKind
{
    /// <summary>Money that left an account.</summary>
    Outflow = 1,

    /// <summary>Money that came into an account.</summary>
    Inflow = 2,

    /// <summary>Money moved between two of the person's own accounts: neither income nor spending.</summary>
    Transfer = 3,
}

/// <summary>
/// The part of life an entry belongs to. Personal is always there; the other two are optional modules.
/// The numbers are stored in the database: never renumber them.
/// </summary>
public enum EntryScope
{
    /// <summary>The person's own money: salary, food, bills.</summary>
    Personal = 1,

    /// <summary>A place rented by the night: bookings, cleaning, platform fees.</summary>
    Rental = 2,

    /// <summary>A property bought to renovate and sell.</summary>
    Flips = 3,
}

/// <summary>
/// The kind of account. The numbers are stored in the database: never renumber them.
/// </summary>
public enum AccountKind
{
    /// <summary>The everyday account.</summary>
    Current = 1,

    /// <summary>Savings.</summary>
    Savings = 2,

    /// <summary>A broker or fund account.</summary>
    Investments = 3,

    /// <summary>A meal card, topped up by the employer and spent on food.</summary>
    MealCard = 4,
}

/// <summary>
/// When the instalments after the first one are paid. The numbers travel in requests: never renumber them.
/// </summary>
public enum InstalmentRule
{
    /// <summary>On the first business day of each month (weekends and Portuguese national holidays skipped).</summary>
    FirstBusinessDay = 1,

    /// <summary>On the same day of each month as the first one (the last day for a shorter month).</summary>
    SameDay = 2,
}

/// <summary>
/// Where a buy-and-sell project stands.
/// </summary>
public enum ProjectState
{
    /// <summary>Bought (or being bought) and not sold yet.</summary>
    InProgress,

    /// <summary>Sold: its return is final.</summary>
    Sold,
}
