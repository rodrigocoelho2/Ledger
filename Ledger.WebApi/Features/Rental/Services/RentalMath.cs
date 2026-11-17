using Ledger.Library.Categories;
using Ledger.Library.Enums;
using Ledger.WebApi.Features.Entries.Models.Entities;
using Ledger.WebApi.Features.Entries.Services;
using Ledger.WebApi.Features.Rental.Models.DTOs;

namespace Ledger.WebApi.Features.Rental.Services;

/// <summary>
/// The sums of the short-term rental module. No database, no clock.
/// </summary>
internal static class RentalMath
{
    /// <summary>The longest stay looked for before a month, when collecting the bookings that reach into it.</summary>
    public const int LookBackDays = 92;

    /// <summary>Whether an entry is a booking: a rental inflow in the bookings category.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>True for a booking.</returns>
    public static bool IsBooking(EntryRow entry) =>
        entry.Kind == (byte)EntryKind.Inflow
        && entry.Scope == (byte)EntryScope.Rental
        && string.Equals(entry.Category, Categories.Bookings, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Places a booking in a month: which of its nights fall in it. A night belongs to the day it starts on, so a stay
    /// that checks in on the 30th for three nights has two nights in a 31-day month and one in the next.
    /// </summary>
    /// <param name="entry">The booking.</param>
    /// <param name="monthStart">The first day of the month.</param>
    /// <returns>The booking as the month sees it.</returns>
    public static BookingDTO Place(EntryRow entry, DateOnly monthStart)
    {
        var nights = Math.Max(0, (int)(entry.Nights ?? 0));
        var checkIn = entry.Day;
        var checkOut = checkIn.AddDays(nights);
        var monthEnd = Months.End(monthStart);

        var firstNight = checkIn < monthStart ? monthStart : checkIn;
        var lastNight = checkOut.AddDays(-1) > monthEnd ? monthEnd : checkOut.AddDays(-1);
        var span = nights == 0 || lastNight < firstNight ? 0 : lastNight.DayNumber - firstNight.DayNumber + 1;

        return new BookingDTO
        {
            Id = entry.EntryUniqueID.ToString("N"),
            Name = entry.Name,
            Channel = entry.Channel,
            CheckIn = checkIn,
            CheckOut = checkOut,
            Nights = nights,
            Amount = entry.Amount,
            NightlyRate = nights == 0 ? null : decimal.Round(entry.Amount / nights, 2, MidpointRounding.AwayFromZero),
            StartDay = span == 0 ? 0 : firstNight.Day,
            Span = span,
            PaidThisMonth = checkIn >= monthStart && checkIn <= monthEnd,
        };
    }

    /// <summary>
    /// How many nights of a month are booked. Two bookings on the same night count once, so the share never passes
    /// every night of the month.
    /// </summary>
    /// <param name="bookings">The bookings, placed in the month.</param>
    /// <param name="nightsInMonth">How many nights the month has.</param>
    /// <returns>The booked nights.</returns>
    public static int NightsBooked(IEnumerable<BookingDTO> bookings, int nightsInMonth)
    {
        var taken = new bool[nightsInMonth + 1];
        foreach (var booking in bookings)
        {
            for (var day = booking.StartDay; booking.Span > 0 && day < booking.StartDay + booking.Span && day <= nightsInMonth; day++)
            {
                taken[day] = true;
            }
        }

        return taken.Count(static night => night);
    }
}
