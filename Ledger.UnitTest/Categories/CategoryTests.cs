using System.Text.RegularExpressions;
using Ledger.Library.Categories;
using Ledger.Library.Enums;

namespace Ledger.UnitTest.Categories;

public sealed class CategoryTests
{
    [Fact]
    public void EveryCategory_HasAUniqueKeyBothNamesAndAColour()
    {
        var all = Ledger.Library.Categories.Categories.All;
        Assert.Equal(all.Count, all.Select(static category => category.Key + "/" + category.Kind).Distinct().Count());
        Assert.All(all, static category =>
        {
            Assert.False(string.IsNullOrWhiteSpace(category.English));
            Assert.False(string.IsNullOrWhiteSpace(category.Portuguese));
            Assert.Matches(new Regex("^#[0-9A-F]{6}$"), category.Color);
            Assert.NotEmpty(category.Scopes);
        });
    }

    [Fact]
    public void For_GivesOnlyTheCategoriesOfThatPartOfLife()
    {
        var rental = Ledger.Library.Categories.Categories.For(EntryKind.Outflow, EntryScope.Rental).Select(static category => category.Key).ToList();
        Assert.Contains("cleaning", rental);
        Assert.Contains("bills", rental);
        Assert.DoesNotContain("gym", rental);
        Assert.DoesNotContain("works", rental);

        var personal = Ledger.Library.Categories.Categories.For(EntryKind.Outflow, EntryScope.Personal).Select(static category => category.Key).ToList();
        Assert.Contains("gym", personal);
        Assert.DoesNotContain("cleaning", personal);
    }

    [Theory]
    [InlineData("groceries", "groceries")]
    [InlineData("Alimentação", "groceries")]
    [InlineData("alimentacao", "groceries")]
    [InlineData("Gym & fitness", "gym")]
    [InlineData("GINÁSIO & DESPORTO", "gym")]
    public void Match_FindsByKeyOrNameInEitherLanguage(string text, string key) =>
        Assert.Equal(key, Ledger.Library.Categories.Categories.Match(text, EntryKind.Outflow)?.Key);

    [Fact]
    public void Name_IsInTheLanguageAsked()
    {
        Assert.Equal("Gym & fitness", Ledger.Library.Categories.Categories.Name("gym", "en"));
        Assert.Equal("Ginásio & desporto", Ledger.Library.Categories.Categories.Name("gym", "pt"));
        Assert.Equal("unknown-key", Ledger.Library.Categories.Categories.Name("unknown-key", "pt"));
    }

    [Theory]
    [InlineData("almoço com a equipa", EntryKind.Outflow, "groceries")]
    [InlineData("Pingo Doce", EntryKind.Outflow, "groceries")]
    [InlineData("Via Verde", EntryKind.Outflow, "transport")]
    [InlineData("fuel", EntryKind.Outflow, "transport")]
    [InlineData("Ginásio", EntryKind.Outflow, "gym")]
    [InlineData("seguro da casa", EntryKind.Outflow, "insurance")]
    [InlineData("Netflix", EntryKind.Outflow, "leisure")]
    [InlineData("salário", EntryKind.Inflow, "salary")]
    [InlineData("juros", EntryKind.Inflow, "dividends")]
    public void Guess_ReadsTheWords(string text, EntryKind kind, string key) => Assert.Equal(key, CategoryGuess.For(text, kind));

    [Theory]
    [InlineData("something odd", EntryKind.Outflow)]
    [InlineData("salary", EntryKind.Outflow)]
    [InlineData("", EntryKind.Inflow)]
    public void Guess_SaysNothingWhenUnsure(string text, EntryKind kind) => Assert.Null(CategoryGuess.For(text, kind));

    [Fact]
    public void IsMeal_KnowsAMealOut()
    {
        Assert.True(CategoryGuess.IsMeal("Almoço"));
        Assert.True(CategoryGuess.IsMeal("team lunch"));
        Assert.False(CategoryGuess.IsMeal("Continente"));
    }
}
