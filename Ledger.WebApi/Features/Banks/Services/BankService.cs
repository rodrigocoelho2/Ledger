using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Ledger.Library.Categories;
using Ledger.Library.Enums;
using Ledger.Library.Services;
using Ledger.WebApi.Extensions.Globalization;
using Ledger.WebApi.Extensions.Services;
using Ledger.WebApi.Extensions.Time;
using Ledger.WebApi.Features.Accounts.Models.Entities;
using Ledger.WebApi.Features.Accounts.Repositories;
using Ledger.WebApi.Features.Banks.Http;
using Ledger.WebApi.Features.Banks.Models.DTOs;
using Ledger.WebApi.Features.Banks.Models.Entities;
using Ledger.WebApi.Features.Banks.Repositories;
using Ledger.WebApi.Features.Entries.Models.Entities;
using Ledger.WebApi.Features.Entries.Repositories;
using Microsoft.Extensions.Options;

namespace Ledger.WebApi.Features.Banks.Services;

/// <summary>
/// Bank connections: connect an account through Enable Banking, pick the Ledger account it goes into, and bring its
/// transactions in.
/// </summary>
internal interface IBankService
{
    /// <summary>The person's connections.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The status.</returns>
    Task<ServiceApiResponse<BankStatusDTO>> StatusAsync(string? userId);

    /// <summary>The banks of a country.</summary>
    /// <param name="country">The country (default PT).</param>
    /// <returns>The banks.</returns>
    Task<ServiceApiResponse<List<BankChoiceDTO>>> BanksAsync(string? country);

    /// <summary>Starts connecting a bank.</summary>
    /// <param name="item">The bank.</param>
    /// <returns>Where to send the person.</returns>
    Task<ServiceApiResponse<BankStartDTO>> StartAsync(BankStartRequest item);

    /// <summary>Finishes connecting: keeps the accounts the person authorised.</summary>
    /// <param name="item">The code and state.</param>
    /// <returns>The status.</returns>
    Task<ServiceApiResponse<BankStatusDTO>> CompleteAsync(BankCompleteRequest item);

    /// <summary>Says which Ledger account a bank account goes into, and brings its transactions.</summary>
    /// <param name="item">The link and the account.</param>
    /// <returns>What the first sync did.</returns>
    Task<ServiceApiResponse<BankSyncDTO>> AssignAsync(BankAssignRequest item);

    /// <summary>Brings a connected account's latest transactions now.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="linkId">The link.</param>
    /// <returns>What the sync did.</returns>
    Task<ServiceApiResponse<BankSyncDTO>> SyncAsync(string? userId, string? linkId);

    /// <summary>Makes Ledger's balance agree with the bank's by changing the account's opening balance.</summary>
    /// <param name="item">The link.</param>
    /// <returns>The link.</returns>
    Task<ServiceApiResponse<BankLinkDTO>> AlignAsync(BankAlignRequest item);

    /// <summary>Disconnects a bank account (its entries stay).</summary>
    /// <param name="userId">The user.</param>
    /// <param name="linkId">The link.</param>
    /// <returns>The deleted id.</returns>
    Task<ServiceApiResponse<string>> DeleteAsync(string? userId, string? linkId);

    /// <summary>Syncs every account whose turn it is (the background worker).</summary>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>How many accounts were synced.</returns>
    Task<int> SyncDueAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Bank business logic.
/// </summary>
/// <param name="bankRepository">The links.</param>
/// <param name="accountRepository">The accounts.</param>
/// <param name="entryRepository">The entries.</param>
/// <param name="client">Enable Banking.</param>
/// <param name="options">The app's options.</param>
/// <param name="clock">The clock.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class BankService(
    IBankRepository bankRepository,
    IAccountRepository accountRepository,
    IEntryRepository entryRepository,
    IEnableBankingClient client,
    IOptions<LedgerOptions> options,
    IAppClock clock,
    IGlobalizationHelper globalizationHelper) : LedgerServiceBase(globalizationHelper), IBankService
{
    /// <summary>The bank picked first.</summary>
    public const string DefaultBank = "Crédito Agrícola";

    /// <summary>The country picked first.</summary>
    public const string DefaultCountry = "PT";

    /// <summary>The longest consent asked for, in days (banks give 90 or 180).</summary>
    public const int ConsentDays = 180;

    /// <summary>How many days back a sync looks, for transactions booked late.</summary>
    public const int LookBackDays = 14;

    /// <summary>The fewest hours between two unattended syncs (banks allow about four a day).</summary>
    public const int MinSyncHours = 6;

    /// <summary>How long a state is good for.</summary>
    public static readonly TimeSpan StateLife = TimeSpan.FromHours(1);

    /// <summary>What <see cref="BankLinkDTO.LastError"/> says when the consent ended.</summary>
    public const string Expired = "expired";

    private const string FieldBank = "Bank";
    private const string FieldCode = "Code";
    private const string FieldLink = "LinkId";
    private const string FieldAccount = "AccountId";

    private readonly LedgerOptions _options = options.Value;

    // When each link was last tried, so a failing bank isn't asked again every half hour.
    private readonly ConcurrentDictionary<Guid, DateTime> _tried = new();

    /// <summary>Gets the hours between unattended syncs.</summary>
    public int SyncHours => Math.Max(MinSyncHours, _options.BankSyncHours);

    /// <inheritdoc />
    public async Task<ServiceApiResponse<BankStatusDTO>> StatusAsync(string? userId)
    {
        if (!TryUser(userId, out var id))
        {
            return await MissingAsync<BankStatusDTO>(userId);
        }

        return ServiceApiResponse<BankStatusDTO>.Success(await StatusOfAsync(id));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<BankChoiceDTO>>> BanksAsync(string? country)
    {
        if (!_options.BankEnabled)
        {
            return await ProblemAsync<List<BankChoiceDTO>>(FieldBank, nameof(TranslationKeys.BankOff));
        }

        try
        {
            var banks = await client.BanksAsync(Country(country));
            return ServiceApiResponse<List<BankChoiceDTO>>.Success(banks
                .Where(static bank => bank.PsuTypes is null || bank.PsuTypes.Contains("personal", StringComparer.OrdinalIgnoreCase))
                .OrderBy(static bank => bank.Name, StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), true))
                .Select(static bank => new BankChoiceDTO
                {
                    Name = bank.Name,
                    Country = bank.Country,
                    ConsentDays = bank.MaximumConsentValidity is { } seconds ? (int)(seconds / 86400) : null,
                })
                .ToList());
        }
        catch (BankException exception)
        {
            return await ProblemAsync<List<BankChoiceDTO>>(FieldBank, nameof(TranslationKeys.BankFailed), exception.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<BankStartDTO>> StartAsync(BankStartRequest item)
    {
        if (!TryUser(item.UserId, out var userId))
        {
            return await MissingAsync<BankStartDTO>(item.UserId);
        }

        if (!_options.BankEnabled)
        {
            return await ProblemAsync<BankStartDTO>(FieldBank, nameof(TranslationKeys.BankOff));
        }

        var country = Country(item.Country);
        var name = string.IsNullOrWhiteSpace(item.Bank) ? DefaultBank : item.Bank.Trim();
        try
        {
            var bank = (await client.BanksAsync(country)).FirstOrDefault(row => string.Equals(row.Name, name, StringComparison.OrdinalIgnoreCase));
            if (bank is null)
            {
                return await ProblemAsync<BankStartDTO>(FieldBank, nameof(TranslationKeys.BankFailed), name);
            }

            // Ledger's own callback when it is registered at Enable Banking; otherwise the first address that is, and the
            // person pastes where they land.
            var registered = await client.RedirectUrlsAsync();
            var wanted = item.RedirectUrl?.Trim();
            var redirect = registered.FirstOrDefault(url => string.Equals(url.TrimEnd('/'), wanted?.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
            var paste = redirect is null;
            redirect ??= registered.FirstOrDefault() ?? wanted;
            if (string.IsNullOrEmpty(redirect))
            {
                return await ProblemAsync<BankStartDTO>(FieldBank, nameof(TranslationKeys.BankFailed), "no redirect URL registered");
            }

            var days = Math.Min(ConsentDays, bank.MaximumConsentValidity is { } seconds && seconds > 0 ? (int)(seconds / 86400) : 90);
            // A few hours short of the bank's maximum: asking for exactly 180 days is refused (422) as soon as the clock is a
            // second ahead of the bank's, which happens on a PC or VM whose clock drifts.
            var validUntil = new DateTimeOffset(clock.UtcNow, TimeSpan.Zero).AddDays(Math.Max(1, days)).AddHours(-6);
            var state = State(userId, new DateTimeOffset(clock.UtcNow, TimeSpan.Zero).Add(StateLife));
            var url = await client.StartAsync(bank.Name, bank.Country, validUntil, redirect, state);
            return ServiceApiResponse<BankStartDTO>.Success(new BankStartDTO { Url = url, State = state, RedirectUrl = redirect, Paste = paste });
        }
        catch (BankException exception)
        {
            return await ProblemAsync<BankStartDTO>(FieldBank, nameof(TranslationKeys.BankFailed), exception.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<BankStatusDTO>> CompleteAsync(BankCompleteRequest item)
    {
        if (!TryUser(item.UserId, out var userId))
        {
            return await MissingAsync<BankStatusDTO>(item.UserId);
        }

        if (!_options.BankEnabled)
        {
            return await ProblemAsync<BankStatusDTO>(FieldCode, nameof(TranslationKeys.BankOff));
        }

        var (code, state, error) = ReadCallback(item.Code, item.State);
        if (error is not null)
        {
            return await ProblemAsync<BankStatusDTO>(FieldCode, nameof(TranslationKeys.BankFailed), error);
        }

        if (code is null || !StateFits(state, userId, new DateTimeOffset(clock.UtcNow, TimeSpan.Zero)))
        {
            return await ProblemAsync<BankStatusDTO>(FieldCode, nameof(TranslationKeys.BankStateInvalid));
        }

        try
        {
            var session = await client.SessionAsync(code);
            var renewed = new List<Guid>();
            foreach (var account in session.Accounts.Where(static account => !string.IsNullOrEmpty(account.Uid)))
            {
                var iban = account.AccountId?.Iban?.Replace(" ", string.Empty, StringComparison.Ordinal);
                var key = await bankRepository.SaveAsync(userId, new BankLinkRow
                {
                    SessionID = session.SessionId,
                    AccountUid = account.Uid,
                    BankName = session.Aspsp?.Name ?? DefaultBank,
                    BankCountry = session.Aspsp?.Country is { Length: 2 } country ? country.ToUpperInvariant() : DefaultCountry,
                    AccountLabel = Label(account),
                    IbanTail = iban is { Length: >= 4 } ? iban[^4..] : null,
                    ValidUntil = session.Access?.ValidUntil?.UtcDateTime,
                });
                if (key is { } linkId)
                {
                    renewed.Add(linkId);
                }
            }

            // A renewed consent of an account already going somewhere catches up straight away.
            foreach (var link in (await bankRepository.ListAsync(userId)).Where(link => renewed.Contains(link.BankLinkUniqueID) && link.AccountUniqueID is not null))
            {
                await RunAsync(link, CancellationToken.None);
            }

            return ServiceApiResponse<BankStatusDTO>.Success(await StatusOfAsync(userId));
        }
        catch (BankException exception)
        {
            return await ProblemAsync<BankStatusDTO>(FieldCode, nameof(TranslationKeys.BankFailed), exception.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<BankSyncDTO>> AssignAsync(BankAssignRequest item)
    {
        if (!TryUser(item.UserId, out var userId) || !Guid.TryParse(item.LinkId, out var linkId))
        {
            return await ProblemAsync<BankSyncDTO>(FieldLink, nameof(TranslationKeys.BankLinkUnknown));
        }

        var link = (await bankRepository.ListAsync(userId)).FirstOrDefault(row => row.BankLinkUniqueID == linkId);
        if (link is null)
        {
            return await ProblemAsync<BankSyncDTO>(FieldLink, nameof(TranslationKeys.BankLinkUnknown));
        }

        Guid? accountId = null;
        if (string.Equals(item.AccountId, BankAssignRequest.NewAccount, StringComparison.OrdinalIgnoreCase))
        {
            var name = link.AccountLabel is { Length: > 0 } label ? label : link.BankName;
            accountId = await accountRepository.SaveAsync(userId, null, name.Length > 60 ? name[..60] : name, AccountKind.Current, 0m);
        }
        else if (!string.IsNullOrWhiteSpace(item.AccountId))
        {
            if (!Guid.TryParse(item.AccountId, out var parsed) || (await accountRepository.ListAsync(userId)).All(account => account.AccountUniqueID != parsed))
            {
                return await ProblemAsync<BankSyncDTO>(FieldAccount, nameof(TranslationKeys.AccountUnknown));
            }

            // One bank account per Ledger account: a second would count every transaction twice.
            if ((await bankRepository.ListAsync(userId)).Any(row => row.AccountUniqueID == parsed && row.BankLinkUniqueID != linkId))
            {
                return await ProblemAsync<BankSyncDTO>(FieldAccount, nameof(TranslationKeys.BankAccountTaken));
            }

            accountId = parsed;
        }

        var from = item.ImportFrom ?? new DateOnly(clock.Today.Year, clock.Today.Month, 1);
        if (from > clock.Today)
        {
            from = clock.Today;
        }

        await bankRepository.AssignAsync(userId, linkId, accountId, accountId is null ? null : from);
        if (accountId is null)
        {
            return ServiceApiResponse<BankSyncDTO>.Success(new BankSyncDTO { Link = await LinkOfAsync(userId, linkId) });
        }

        return await SyncAsync(Id(userId), Id(linkId));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<BankSyncDTO>> SyncAsync(string? userId, string? linkId)
    {
        if (!TryUser(userId, out var id) || !Guid.TryParse(linkId, out var key))
        {
            return await ProblemAsync<BankSyncDTO>(FieldLink, nameof(TranslationKeys.BankLinkUnknown));
        }

        var link = (await bankRepository.ListAsync(id)).FirstOrDefault(row => row.BankLinkUniqueID == key && row.AccountUniqueID is not null);
        if (link is null)
        {
            return await ProblemAsync<BankSyncDTO>(FieldLink, nameof(TranslationKeys.BankLinkUnknown));
        }

        if (!_options.BankEnabled)
        {
            return await ProblemAsync<BankSyncDTO>(FieldLink, nameof(TranslationKeys.BankOff));
        }

        var (imported, matched, transfers, error) = await RunAsync(link, CancellationToken.None);
        if (error is not null && error != Expired)
        {
            return await ProblemAsync<BankSyncDTO>(FieldLink, nameof(TranslationKeys.BankFailed), error);
        }

        return ServiceApiResponse<BankSyncDTO>.Success(new BankSyncDTO { Imported = imported, Matched = matched, Transfers = transfers, Link = await LinkOfAsync(id, key) });
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<BankLinkDTO>> AlignAsync(BankAlignRequest item)
    {
        if (!TryUser(item.UserId, out var userId) || !Guid.TryParse(item.LinkId, out var linkId))
        {
            return await ProblemAsync<BankLinkDTO>(FieldLink, nameof(TranslationKeys.BankLinkUnknown));
        }

        var link = (await bankRepository.ListAsync(userId)).FirstOrDefault(row => row.BankLinkUniqueID == linkId);
        if (link?.AccountUniqueID is not { } accountId || link.BankBalance is not { } balance)
        {
            return await ProblemAsync<BankLinkDTO>(FieldLink, nameof(TranslationKeys.BankLinkUnknown));
        }

        await AlignToAsync(userId, accountId, balance);
        return ServiceApiResponse<BankLinkDTO>.Success((await LinkOfAsync(userId, linkId))!);
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> DeleteAsync(string? userId, string? linkId)
    {
        if (!TryUser(userId, out var id) || !Guid.TryParse(linkId, out var key))
        {
            return await MissingAsync<string>(linkId);
        }

        var link = (await bankRepository.ListAsync(id)).FirstOrDefault(row => row.BankLinkUniqueID == key);
        if (link is null || !await bankRepository.DeleteAsync(id, key))
        {
            return await MissingAsync<string>(linkId);
        }

        // The session ends at the bank too, unless another account still uses it.
        if (_options.BankEnabled && (await bankRepository.ListAsync(id)).All(row => row.SessionID != link.SessionID))
        {
            try
            {
                await client.EndAsync(link.SessionID);
            }
            catch (BankException)
            {
                // The consent ends on its own.
            }
        }

        _tried.TryRemove(key, out _);
        return ServiceApiResponse<string>.Success(Id(key));
    }

    /// <inheritdoc />
    public async Task<int> SyncDueAsync(CancellationToken cancellationToken)
    {
        if (!_options.BankEnabled)
        {
            return 0;
        }

        var now = clock.UtcNow;
        var every = TimeSpan.FromHours(SyncHours);
        var synced = 0;
        foreach (var link in await bankRepository.AllAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var last = link.LastSyncAt;
            if (_tried.TryGetValue(link.BankLinkUniqueID, out var tried) && (last is null || tried > last))
            {
                last = tried;
            }

            if (last is { } at && now - at < every)
            {
                continue;
            }

            if (link.ValidUntil is { } ends && ends <= now && link.LastError == Expired)
            {
                continue;
            }

            await RunAsync(link, cancellationToken);
            synced++;
        }

        return synced;
    }

    /// <summary>Signs a state: the user, until when, and an HMAC with the API key, so only this person can finish.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="until">Until when.</param>
    /// <returns>The state.</returns>
    internal string State(Guid userId, DateTimeOffset until)
    {
        var body = $"{userId:N}.{until.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}";
        return $"{body}.{Sign(body)}";
    }

    /// <summary>Checks a state: signed here, for this person, not too old.</summary>
    /// <param name="state">The state.</param>
    /// <param name="userId">The user finishing.</param>
    /// <param name="now">Now.</param>
    /// <returns>True when it fits.</returns>
    internal bool StateFits(string? state, Guid userId, DateTimeOffset now)
    {
        var parts = (state ?? string.Empty).Split('.');
        if (parts.Length != 3 || !Guid.TryParseExact(parts[0], "N", out var owner) || owner != userId
            || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var until) || now.ToUnixTimeSeconds() > until)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Sign($"{parts[0]}.{parts[1]}")), Encoding.ASCII.GetBytes(parts[2]));
    }

    /// <summary>Reads what came back: a code and state, or a whole pasted address with them (or an error) in its query.</summary>
    /// <param name="code">The code, or the address.</param>
    /// <param name="state">The state, when given apart.</param>
    /// <returns>The code, the state and the bank's error.</returns>
    internal static (string? Code, string? State, string? Error) ReadCallback(string? code, string? state)
    {
        var text = (code ?? string.Empty).Trim();
        if (!text.Contains('?', StringComparison.Ordinal) && !text.Contains('=', StringComparison.Ordinal))
        {
            return (text.Length == 0 ? null : text, state?.Trim(), null);
        }

        var query = text[(text.IndexOf('?', StringComparison.Ordinal) + 1)..];
        var hash = query.IndexOf('#', StringComparison.Ordinal);
        if (hash >= 0)
        {
            query = query[..hash];
        }

        var values = query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(static pair => pair.Split('=', 2))
            .GroupBy(static pair => Uri.UnescapeDataString(pair[0]), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First().Length > 1 ? Uri.UnescapeDataString(group.First()[1].Replace('+', ' ')) : string.Empty, StringComparer.OrdinalIgnoreCase);
        if (values.TryGetValue("error", out var error))
        {
            return (null, null, values.TryGetValue("error_description", out var description) && description.Length > 0 ? description : error);
        }

        return (values.TryGetValue("code", out var found) && found.Length > 0 ? found : null, values.TryGetValue("state", out var back) ? back : state?.Trim(), null);
    }

    private static string Country(string? country) => country is { Length: 2 } code ? code.ToUpperInvariant() : DefaultCountry;

    private static string? Label(EbAccount account)
    {
        var label = account.Name ?? account.Product;
        return string.IsNullOrWhiteSpace(label) ? null : label.Trim().Length > 120 ? label.Trim()[..120] : label.Trim();
    }

    private string Sign(string body)
    {
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("ledger-bank:" + _options.ApiKey), Encoding.UTF8.GetBytes(body));
        return Convert.ToBase64String(mac, 0, 18).Replace('+', '-').Replace('/', '_');
    }

    /// <summary>One sync of one link; never throws: a failure is kept on the link.</summary>
    private async Task<(int Imported, int Matched, int Transfers, string? Error)> RunAsync(BankLinkRow link, CancellationToken cancellationToken)
    {
        if (link.AccountUniqueID is not { } accountId)
        {
            return (0, 0, 0, null);
        }

        var now = clock.UtcNow;
        _tried[link.BankLinkUniqueID] = now;
        if (link.ValidUntil is { } ends && ends <= now)
        {
            await bankRepository.SyncedAsync(link.BankLinkUniqueID, now, Expired, null);
            return (0, 0, 0, Expired);
        }

        try
        {
            var userId = link.UserUniqueID;
            var today = clock.Today;
            var importFrom = link.ImportFrom is { } start ? DateOnly.FromDateTime(start) : new DateOnly(today.Year, today.Month, 1);
            var first = link.LastSyncAt is null;
            var from = first ? importFrom : today.AddDays(-LookBackDays);
            if (from < importFrom)
            {
                from = importFrom;
            }

            var moves = BankMath.Read(link.BankLinkUniqueID, await client.TransactionsAsync(link.AccountUid, from, today, cancellationToken), today);
            var balance = BankMath.Balance(await client.BalancesAsync(link.AccountUid, cancellationToken));

            var around = await entryRepository.ListAsync(userId, from.AddDays(-BankMath.MatchDays), today.AddDays(BankMath.MatchDays));
            var known = around.SelectMany(static entry => new[] { entry.ExternalID, entry.ToExternalID }).OfType<string>().ToHashSet(StringComparer.Ordinal);
            var held = BankMath.Held(accountId, around);
            var others = (await bankRepository.ListAsync(userId))
                .Where(row => row.BankLinkUniqueID != link.BankLinkUniqueID && row.AccountUniqueID is not null)
                .Select(static row => row.AccountUniqueID!.Value)
                .ToHashSet();
            var taken = new HashSet<(Guid, bool)>();
            var matched = 0;
            var transfers = 0;
            var rows = new List<EntryRow>();
            foreach (var move in moves.Where(move => !known.Contains(move.ExternalId) && !held.Contains(BankMath.Bare(move.ExternalId))))
            {
                // Written by hand already (a transfer between two connected accounts matches on both sides).
                if (BankMath.Match(move, accountId, around, taken) is { } entry)
                {
                    var arriving = BankMath.Arriving(entry, accountId);
                    taken.Add((entry.EntryUniqueID, arriving));
                    await entryRepository.AttachExternalAsync(userId, entry.EntryUniqueID, move.ExternalId, arriving);
                    matched++;
                    continue;
                }

                // The other half of money moved between two connected accounts: one transfer, not spending and income.
                if (others.Count > 0 && BankMath.Pair(move, accountId, others, around, taken) is { } half)
                {
                    var (source, target, leaving, arriving) = move.Kind == EntryKind.Inflow
                        ? (half.AccountUniqueID!.Value, accountId, half.ExternalID!, move.ExternalId)
                        : (accountId, half.AccountUniqueID!.Value, move.ExternalId, half.ExternalID!);
                    if (await entryRepository.MakeTransferAsync(userId, half.EntryUniqueID, source, target, leaving, arriving))
                    {
                        taken.Add((half.EntryUniqueID, false));
                        transfers++;
                        continue;
                    }
                }

                rows.Add(new EntryRow
                {
                    Kind = (byte)move.Kind,
                    Scope = (byte)EntryScope.Personal,
                    Name = move.Name,
                    Amount = move.Amount,
                    EntryDate = move.Day.ToDateTime(TimeOnly.MinValue),
                    Category = await CategoryAsync(userId, move),
                    AccountUniqueID = accountId,
                    ExternalID = move.ExternalId,
                    Review = true,
                });
            }

            var imported = rows.Count == 0 ? 0 : await entryRepository.ImportAsync(userId, rows);

            // The first sync makes Ledger agree with the bank: the opening balance takes up what came before.
            if (first && balance is { } bankBalance)
            {
                await AlignToAsync(userId, accountId, bankBalance);
            }

            await bankRepository.SyncedAsync(link.BankLinkUniqueID, clock.UtcNow, null, balance);
            return (imported, matched, transfers, null);
        }
        catch (BankException exception)
        {
            var message = exception.Message.Length > 400 ? exception.Message[..400] : exception.Message;
            await bankRepository.SyncedAsync(link.BankLinkUniqueID, clock.UtcNow, message, null);
            return (0, 0, 0, message);
        }
    }

    /// <summary>A category for a bank move: what the same name had last time, else a guess from the words.</summary>
    private async Task<string> CategoryAsync(Guid userId, BankMove move)
    {
        var last = await entryRepository.SuggestAsync(userId, move.Name);
        if (last is not null && last.Kind == (byte)move.Kind && Categories.Find(last.Category) is { } remembered)
        {
            return remembered.Key;
        }

        return CategoryGuess.For(move.Name, move.Kind) ?? CategoryGuess.For(move.Text, move.Kind) ?? Categories.Fallback(move.Kind);
    }

    private async Task AlignToAsync(Guid userId, Guid accountId, decimal bankBalance)
    {
        var account = (await accountRepository.ListAsync(userId)).FirstOrDefault(row => row.AccountUniqueID == accountId);
        if (account is null || account.ValueSource is not null)
        {
            return;
        }

        var opening = bankBalance - account.Movement;
        if (opening != account.OpeningBalance && Math.Abs(opening) <= MaxAmount)
        {
            await accountRepository.SaveAsync(userId, accountId, account.Name, (AccountKind)account.Kind, opening);
        }
    }

    private async Task<BankStatusDTO> StatusOfAsync(Guid userId)
    {
        var accounts = await accountRepository.ListAsync(userId);
        return new BankStatusDTO
        {
            Enabled = _options.BankEnabled,
            SyncHours = SyncHours,
            Links = (await bankRepository.ListAsync(userId)).Select(link => ToDTO(link, accounts, clock.UtcNow)).ToList(),
        };
    }

    private async Task<BankLinkDTO?> LinkOfAsync(Guid userId, Guid linkId)
    {
        var accounts = await accountRepository.ListAsync(userId);
        return (await bankRepository.ListAsync(userId)).Where(link => link.BankLinkUniqueID == linkId).Select(link => ToDTO(link, accounts, clock.UtcNow)).FirstOrDefault();
    }

    /// <summary>Maps a link to what the Website sees.</summary>
    /// <param name="link">The link.</param>
    /// <param name="accounts">The person's accounts.</param>
    /// <param name="now">Now (UTC).</param>
    /// <returns>The link.</returns>
    internal static BankLinkDTO ToDTO(BankLinkRow link, IReadOnlyList<AccountRow> accounts, DateTime now)
    {
        var account = link.AccountUniqueID is { } id ? accounts.FirstOrDefault(row => row.AccountUniqueID == id) : null;
        return new BankLinkDTO
        {
            Id = Id(link.BankLinkUniqueID),
            BankName = link.BankName,
            AccountLabel = link.AccountLabel,
            IbanTail = link.IbanTail,
            AccountId = account is null ? null : Id(account.AccountUniqueID),
            AccountName = account?.Name,
            ValidUntil = link.ValidUntil is { } until ? new DateTimeOffset(DateTime.SpecifyKind(until, DateTimeKind.Utc)) : null,
            Expired = link.ValidUntil is { } ends && ends <= now,
            ImportFrom = link.ImportFrom is { } from ? DateOnly.FromDateTime(from) : null,
            LastSyncAt = link.LastSyncAt is { } at ? new DateTimeOffset(DateTime.SpecifyKind(at, DateTimeKind.Utc)) : null,
            LastError = link.LastError,
            BankBalance = link.BankBalance,
            LedgerBalance = account?.Balance,
            Difference = account is not null && link.BankBalance is { } bank ? account.Balance - bank : null,
        };
    }
}
