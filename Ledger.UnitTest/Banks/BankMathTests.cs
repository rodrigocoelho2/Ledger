using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ledger.Library.Enums;
using Ledger.WebApi.Features.Accounts.Models.Entities;
using Ledger.WebApi.Features.Banks.Http;
using Ledger.WebApi.Features.Banks.Services;
using Ledger.WebApi.Features.Overview.Services;

namespace Ledger.UnitTest.Banks;

public sealed class BankMathTests
{
    private static readonly Guid Link = Guid.Parse("0123456789abcdef0123456789abcdef");
    private static readonly DateOnly Today = new(2026, 11, 17);

    [Fact]
    public void Read_KeepsBookedMoves_WithKindsAndPrefixedIds()
    {
        var pending = FakeBank.Move("p1", -5m, Today, "Café");
        pending.Status = "PDNG";
        var moves = BankMath.Read(Link, [
            FakeBank.Move("r2", -9.8m, new DateOnly(2026, 11, 5), "Restaurante O Pátio"),
            FakeBank.Move("r1", 1500m, new DateOnly(2026, 11, 3), "EMPRESA LDA"),
            pending,
        ], Today);

        Assert.Equal(2, moves.Count);
        Assert.Equal("01234567:r1", moves[0].ExternalId);
        Assert.Equal(EntryKind.Inflow, moves[0].Kind);
        Assert.Equal(1500m, moves[0].Amount);
        Assert.Equal("Empresa Lda", moves[0].Name);
        Assert.Equal("MB Way Joana SA", BankMath.Clean("MB WAY JOANA SA"));
        Assert.Equal(EntryKind.Outflow, moves[1].Kind);
        Assert.Equal(9.8m, moves[1].Amount);
        Assert.Equal(new DateOnly(2026, 11, 5), moves[1].Day);
    }

    [Fact]
    public void Read_GivesRepeatsACounter_AndMovesWithoutIdsAStableHash()
    {
        var day = new DateOnly(2026, 11, 10);
        var first = BankMath.Read(Link, [FakeBank.Move("same", -1m, day, "A"), FakeBank.Move("same", -2m, day, "B"), FakeBank.Move(null, -3m, day, "C")], Today);
        var again = BankMath.Read(Link, [FakeBank.Move(null, -3m, day, "C")], Today);

        Assert.Equal("01234567:same", first[0].ExternalId);
        Assert.Equal("01234567:same#1", first[1].ExternalId);
        Assert.StartsWith("01234567:h", first[2].ExternalId, StringComparison.Ordinal);
        Assert.Equal(first[2].ExternalId, again[0].ExternalId);
        Assert.All(first, static move => Assert.True(move.ExternalId.Length <= BankMath.ExternalIdLength));
    }

    [Theory]
    [InlineData("COMPRA 4512 RESTAURANTE O PATEO LOURES", "Restaurante O Pateo Loures")]
    [InlineData("EREF+12345 SVWZ+Renda Novembro", "12345 Renda Novembro")]
    [InlineData("  MB WAY   Joana ", "MB WAY   Joana")]
    [InlineData("Continente Bom Dia", "Continente Bom Dia")]
    [InlineData("***", "")]
    public void Clean_TidiesBankDescriptions(string text, string expected)
    {
        var cleaned = BankMath.Clean(text);
        Assert.Equal(expected.Replace("   ", " ", StringComparison.Ordinal), cleaned);
    }

    [Fact]
    public void Name_UsesTheOtherSide_ThenTheDescription()
    {
        var withParty = FakeBank.Move("x", -20m, Today, "EDP COMERCIAL", "DD 123");
        var withText = FakeBank.Move("y", -20m, Today, null, "COMPRA 9999 PINGO DOCE ODIVELAS");
        var bare = FakeBank.Move("z", 20m, Today);

        Assert.Equal("EDP Comercial", BankMath.Name(withParty, EntryKind.Outflow));
        Assert.Equal("Pingo Doce Odivelas", BankMath.Name(withText, EntryKind.Outflow));
        Assert.Equal("Bank credit", BankMath.Name(bare, EntryKind.Inflow));
    }

    [Fact]
    public void Balance_PrefersBookedOverAvailable()
    {
        Assert.Equal(1234.5m, BankMath.Balance([
            new EbBalance { BalanceType = "ITAV", BalanceAmount = new EbAmount { Amount = "1500.00" } },
            new EbBalance { BalanceType = "CLBD", BalanceAmount = new EbAmount { Amount = "1234.50" } },
        ]));
        Assert.Equal(10m, BankMath.Balance([new EbBalance { BalanceType = "OTHR", BalanceAmount = new EbAmount { Amount = "10" } }]));
        Assert.Null(BankMath.Balance([]));
    }

    [Fact]
    public void Match_FindsTheHandWrittenEntry_SameWaySameAmountClosestDay()
    {
        var account = Guid.NewGuid();
        var other = Guid.NewGuid();
        var move = new BankMove("x:1", EntryKind.Outflow, 45m, new DateOnly(2026, 11, 11), "Galp");
        var far = Make.Entry(EntryKind.Outflow, 45m, new DateOnly(2026, 11, 7), account: account);
        var near = Make.Entry(EntryKind.Outflow, 45m, new DateOnly(2026, 11, 10), account: account);
        var elsewhere = Make.Entry(EntryKind.Outflow, 45m, new DateOnly(2026, 11, 11), account: other);
        var transferOut = Make.Entry(EntryKind.Transfer, 45m, new DateOnly(2026, 11, 12), account: account, to: other);
        var tied = Make.Entry(EntryKind.Outflow, 45m, new DateOnly(2026, 11, 11), account: account);
        tied.ExternalID = "x:0";

        Assert.Same(near, BankMath.Match(move, account, [far, near, elsewhere, tied], new HashSet<Guid>()));
        Assert.Same(transferOut, BankMath.Match(move, account, [far, transferOut], new HashSet<Guid>()));
        Assert.Null(BankMath.Match(move, account, [far, near], new HashSet<Guid> { near.EntryUniqueID }));
        Assert.Equal(EntryKind.Inflow, BankMath.Moves(transferOut, other));
    }

    [Fact]
    public void Token_IsAnRs256JwtWithTheAppAsKid()
    {
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportPkcs8PrivateKeyPem();
        var now = new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

        var token = EnableBankingClient.Token("app-123", Convert.ToBase64String(Encoding.UTF8.GetBytes(pem)), now);
        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);

        using var header = JsonDocument.Parse(Decode(parts[0]));
        Assert.Equal("RS256", header.RootElement.GetProperty("alg").GetString());
        Assert.Equal("app-123", header.RootElement.GetProperty("kid").GetString());
        using var claims = JsonDocument.Parse(Decode(parts[1]));
        Assert.Equal("api.enablebanking.com", claims.RootElement.GetProperty("aud").GetString());
        Assert.Equal(now.AddHours(1).ToUnixTimeSeconds(), claims.RootElement.GetProperty("exp").GetInt64());
        Assert.True(rsa.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Decode(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        // The PEM itself, with the line breaks written out as in an .env, works too.
        Assert.Equal(3, EnableBankingClient.Token("app-123", pem.Replace("\n", "\\n", StringComparison.Ordinal), now).Split('.').Length);
    }

    [Fact]
    public void ValueChange_IsTheMonthsLastValueLessTheOneBefore()
    {
        var account = Guid.NewGuid();
        AccountValueRow Value(int month, int day, decimal value) => new() { AccountUniqueID = account, ValueDate = new DateTime(2026, month, day), Value = value };
        var start = new DateOnly(2026, 11, 1);
        var end = new DateOnly(2026, 11, 30);

        Assert.Equal(250m, OverviewMath.ValueChange([Value(10, 31, 10_000m), Value(11, 5, 10_100m), Value(11, 17, 10_250m)], start, end));
        Assert.Null(OverviewMath.ValueChange([Value(11, 5, 10_100m)], start, end));
    }

    private static byte[] Decode(string part)
    {
        var text = part.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(text.PadRight(text.Length + ((4 - (text.Length % 4)) % 4), '='));
    }
}
