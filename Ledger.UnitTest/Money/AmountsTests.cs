using Ledger.Library.Money;
using Ledger.WebApi.Features.Entries.Services;

namespace Ledger.UnitTest.Money;

public sealed class AmountsTests
{
    [Theory]
    [InlineData("9.80", 9.80)]
    [InlineData("9,80", 9.80)]
    [InlineData("1960", 1960)]
    [InlineData("1.960", 1960)]
    [InlineData("1,960", 1960)]
    [InlineData("1.960,00", 1960)]
    [InlineData("1,960.00", 1960)]
    [InlineData("1.234.567,89", 1234567.89)]
    [InlineData("€ 12,40", 12.40)]
    [InlineData("0.125", 0.13)]
    [InlineData("0,5", 0.5)]
    public void TryParse_ReadsHowPeopleType(string text, double expected)
    {
        Assert.True(Amounts.TryParse(text, out var amount));
        Assert.Equal((decimal)expected, amount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("lunch")]
    [InlineData(",.")]
    public void TryParse_RefusesWhatIsNotANumber(string? text) => Assert.False(Amounts.TryParse(text, out _));
}

public sealed class QuickAddTests
{
    [Theory]
    [InlineData("9.80 lunch", 9.80, "lunch", false)]
    [InlineData("lunch 9,80", 9.80, "lunch", false)]
    [InlineData("+1960 salary", 1960, "salary", true)]
    [InlineData("  45   gym   membership ", 45, "gym membership", false)]
    [InlineData("12,40 almoço", 12.40, "almoço", false)]
    public void Parse_FindsTheAmountAndTheWords(string text, double amount, string name, bool plus)
    {
        var (understood, parsedAmount, parsedName, parsedPlus) = QuickAdd.Parse(text);
        Assert.True(understood);
        Assert.Equal((decimal)amount, parsedAmount);
        Assert.Equal(name, parsedName);
        Assert.Equal(plus, parsedPlus);
    }

    [Theory]
    [InlineData("lunch")]
    [InlineData("")]
    [InlineData("0 lunch")]
    public void Parse_NeedsAnAmountAboveZero(string text) => Assert.False(QuickAdd.Parse(text).Understood);
}
