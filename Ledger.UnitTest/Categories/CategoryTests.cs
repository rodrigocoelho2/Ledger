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
    [InlineData("almoço com a equipa", EntryKind.Outflow, "eating-out")]
    [InlineData("Pingo Doce", EntryKind.Outflow, "groceries")]
    [InlineData("Pingo Docelisb", EntryKind.Outflow, "groceries")]
    [InlineData("Mcdonalds Loures", EntryKind.Outflow, "eating-out")]
    [InlineData("Pad Port Infantado", EntryKind.Outflow, "eating-out")]
    [InlineData("Prozis Com SA Canical", EntryKind.Outflow, "gym")]
    [InlineData("Anthropic Claude Sub Dublin", EntryKind.Outflow, "subscriptions")]
    [InlineData("Apple.Com/Bill", EntryKind.Outflow, "subscriptions")]
    [InlineData("PLAYSTATIONHilversum", EntryKind.Outflow, "leisure")]
    [InlineData("MGP Vinted", EntryKind.Outflow, "online-shopping")]
    [InlineData("WWW.AMAZON Y79N34EX5 amzn.com/bill", EntryKind.Outflow, "online-shopping")]
    [InlineData("Fnac Ubbo", EntryKind.Outflow, "shopping")]
    [InlineData("Boulevard Legend", EntryKind.Outflow, "gym")]
    [InlineData("A.S.Loures", EntryKind.Outflow, "transport")]
    [InlineData("Ubr Pending.Uber.Com", EntryKind.Outflow, "transport")]
    [InlineData("Cetelem", EntryKind.Outflow, "loans")]
    [InlineData("Transferencia com cartao debit", EntryKind.Outflow, "bank-fees")]
    [InlineData("Devolução El Corte Ingles Lisboa", EntryKind.Inflow, "refunds")]
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

    [Theory]
    [InlineData("Trf imediata DIANA ISABEL TOM S CABRITA", true, true)]
    [InlineData("Margarida Monteiro Gon?Alves", false, true)]
    [InlineData("Trf imediata SALERO LDA", true, false)]
    [InlineData("Fin Solutia Estado", false, false)]
    [InlineData("El Corte Ingles Lisboa", true, false)]
    [InlineData("Cetelem", false, false)]
    public void IsPerson_TellsPeopleFromBusinesses(string name, bool sent, bool person) =>
        Assert.Equal(person, CategoryGuess.IsPerson(name, sent));

    [Fact]
    public void IsMeal_KnowsAMealOut()
    {
        Assert.True(CategoryGuess.IsMeal("Almoço"));
        Assert.True(CategoryGuess.IsMeal("team lunch"));
        Assert.False(CategoryGuess.IsMeal("Continente"));
    }
}
