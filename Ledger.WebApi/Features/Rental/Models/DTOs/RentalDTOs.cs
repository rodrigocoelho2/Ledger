using Ledger.WebApi.Features.Entries.Models.DTOs;
using Ledger.WebApi.Features.Overview.Models.DTOs;

namespace Ledger.WebApi.Features.Rental.Models.DTOs;

/// <summary>A booking: an inflow with a check-in and a number of nights.</summary>
public sealed class BookingDTO
{
    /// <summary>Gets or sets the entry's identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets what the person wrote.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets where it came from (Airbnb, Booking, direct…).</summary>
    public string? Channel { get; set; }

    /// <summary>Gets or sets the first night.</summary>
    public DateOnly CheckIn { get; set; }

    /// <summary>Gets or sets the morning the guests leave.</summary>
    public DateOnly CheckOut { get; set; }

    /// <summary>Gets or sets how many nights (0: not said).</summary>
    public int Nights { get; set; }

    /// <summary>Gets or sets what the guests paid.</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets the price of a night (null: nights not said).</summary>
    public decimal? NightlyRate { get; set; }

    /// <summary>Gets or sets the day of the month being looked at where its bar starts (1-based; 0: none of its nights are in it).</summary>
    public int StartDay { get; set; }

    /// <summary>Gets or sets how many of its nights are in the month being looked at.</summary>
    public int Span { get; set; }

    /// <summary>Gets or sets a value indicating whether the money came in during the month being looked at.</summary>
    public bool PaidThisMonth { get; set; }
}

/// <summary>A month of a place rented by the night.</summary>
public sealed class RentalDTO
{
    /// <summary>Gets or sets the month (yyyy-MM).</summary>
    public string Month { get; set; } = string.Empty;

    /// <summary>Gets or sets the month before it (yyyy-MM).</summary>
    public string PreviousMonth { get; set; } = string.Empty;

    /// <summary>Gets or sets the month after it (yyyy-MM).</summary>
    public string NextMonth { get; set; } = string.Empty;

    /// <summary>Gets or sets what the guests paid.</summary>
    public decimal Revenue { get; set; }

    /// <summary>Gets or sets what the place cost.</summary>
    public decimal Costs { get; set; }

    /// <summary>Gets or sets revenue minus costs.</summary>
    public decimal Margin { get; set; }

    /// <summary>Gets or sets the margin as a share of revenue (null: no revenue).</summary>
    public decimal? MarginShare { get; set; }

    /// <summary>Gets or sets the costs as a share of revenue (null: no revenue).</summary>
    public decimal? CostShare { get; set; }

    /// <summary>Gets or sets how many bookings were paid in the month.</summary>
    public int BookingCount { get; set; }

    /// <summary>Gets or sets what a booking paid on average (null: no bookings).</summary>
    public decimal? AverageBooking { get; set; }

    /// <summary>Gets or sets how many nights of the month are booked.</summary>
    public int NightsBooked { get; set; }

    /// <summary>Gets or sets how many nights the month has.</summary>
    public int NightsInMonth { get; set; }

    /// <summary>Gets or sets the share of the month's nights that are booked (0 to 1).</summary>
    public decimal Occupancy { get; set; }

    /// <summary>Gets or sets the price of a night, over the bookings paid in the month that say their nights (null: none do).</summary>
    public decimal? NightlyRate { get; set; }

    /// <summary>Gets or sets the bookings with nights in the month or paid in it, by check-in.</summary>
    public List<BookingDTO> Bookings { get; set; } = [];

    /// <summary>Gets or sets where the revenue went: each cost category, with its share of revenue.</summary>
    public List<FlowPartDTO> Split { get; set; } = [];

    /// <summary>Gets or sets the year of the month.</summary>
    public int Year { get; set; }

    /// <summary>Gets or sets the margin of that year so far.</summary>
    public decimal YearMargin { get; set; }

    /// <summary>Gets or sets the months of that year with rental entries, oldest first.</summary>
    public List<MonthNetDTO> YearMonths { get; set; } = [];

    /// <summary>Gets or sets every rental entry of the month, newest first.</summary>
    public List<EntryDTO> Entries { get; set; } = [];
}
