namespace Ledger.Library.Calendar;

/// <summary>
/// Business days in Portugal: Monday to Friday, without the national holidays. Banks move money on these days, so an
/// instalment "on the first business day of the month" lands on the first one.
/// </summary>
public static class BusinessDays
{
    /// <summary>Whether a day is a business day.</summary>
    /// <param name="day">The day.</param>
    /// <returns>True from Monday to Friday, when it isn't a national holiday.</returns>
    public static bool Is(DateOnly day) => day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !IsHoliday(day);

    /// <summary>The first business day of a month.</summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month (1 to 12).</param>
    /// <returns>The day.</returns>
    public static DateOnly FirstOfMonth(int year, int month) => OnOrAfter(new DateOnly(year, month, 1));

    /// <summary>The first business day of the month a day is in.</summary>
    /// <param name="day">Any day of the month.</param>
    /// <returns>The day.</returns>
    public static DateOnly FirstOfMonth(DateOnly day) => FirstOfMonth(day.Year, day.Month);

    /// <summary>The day itself if it is a business day, otherwise the next one.</summary>
    /// <param name="day">The day.</param>
    /// <returns>The business day.</returns>
    public static DateOnly OnOrAfter(DateOnly day)
    {
        while (!Is(day))
        {
            day = day.AddDays(1);
        }

        return day;
    }

    /// <summary>Whether a day is a Portuguese national holiday.</summary>
    /// <param name="day">The day.</param>
    /// <returns>True for a national holiday.</returns>
    public static bool IsHoliday(DateOnly day)
    {
        switch ((day.Month, day.Day))
        {
            case (1, 1):   // Ano Novo
            case (4, 25):  // Dia da Liberdade
            case (5, 1):   // Dia do Trabalhador
            case (6, 10):  // Dia de Portugal
            case (8, 15):  // Assunção de Nossa Senhora
            case (10, 5):  // Implantação da República
            case (11, 1):  // Todos os Santos
            case (12, 1):  // Restauração da Independência
            case (12, 8):  // Imaculada Conceição
            case (12, 25): // Natal
                return true;
        }

        var easter = Easter(day.Year);
        return day == easter.AddDays(-2)   // Sexta-feira Santa
            || day == easter               // Páscoa
            || day == easter.AddDays(60);  // Corpo de Deus
    }

    /// <summary>Easter Sunday (Gregorian calendar, the anonymous algorithm).</summary>
    /// <param name="year">The year.</param>
    /// <returns>The day.</returns>
    public static DateOnly Easter(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = ((19 * a) + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;
        var month = (h + l - (7 * m) + 114) / 31;
        var dayOfMonth = ((h + l - (7 * m) + 114) % 31) + 1;
        return new DateOnly(year, month, dayOfMonth);
    }
}
