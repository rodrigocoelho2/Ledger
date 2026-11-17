using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ledger.Library.Enums;
using Ledger.Library.Validation;
using Ledger.Sdk.Models;

namespace Ledger.Sdk;

/// <summary>
/// Typed client of Ledger.WebApi. Method names follow the API operation ids ({Feature}{Operation}Async).
/// Register with <see cref="ServiceCollectionExtensions.AddLedgerClient"/>, which sets the base address and the x-api-key header.
/// </summary>
/// <param name="httpClient">The configured HttpClient.</param>
public sealed class LedgerClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    // ----- User -----

    /// <summary>How many accounts exist, and the demo account in demo mode.</summary>
    /// <returns>The status.</returns>
    public Task<UserStatusDTO> UserStatusAsync() => GetAsync<UserStatusDTO>("user/status");

    /// <summary>Lists every account.</summary>
    /// <returns>The accounts.</returns>
    public Task<List<UserDTO>> UserListAsync() => GetAsync<List<UserDTO>>("user/list");

    /// <summary>Gets one account.</summary>
    /// <param name="identifier">The account id.</param>
    /// <returns>The account.</returns>
    public Task<UserDTO> UserGetAsync(string identifier) => GetAsync<UserDTO>($"user?identifier={Uri.EscapeDataString(identifier)}");

    /// <summary>Creates an account (the first one is an admin).</summary>
    /// <param name="item">User name and password.</param>
    /// <returns>The account.</returns>
    public Task<UserDTO> UserCreateAsync(UserCreateRequest item) => PostAsync<UserDTO>("user", item);

    /// <summary>Checks a user name and password.</summary>
    /// <param name="item">User name and password.</param>
    /// <returns>The account.</returns>
    public Task<UserDTO> UserSignInAsync(UserSignInRequest item) => PostAsync<UserDTO>("user/signin", item);

    /// <summary>Finds the account a Discord account is linked to (a <see cref="LedgerApiException"/> with 404 when none).</summary>
    /// <param name="item">The Discord account's id.</param>
    /// <returns>The account.</returns>
    public Task<UserDTO> UserDiscordSignInAsync(UserDiscordSignInRequest item) => PostAsync<UserDTO>("user/discord/signin", item);

    /// <summary>Creates an account that signs in with Discord (no password until one is set).</summary>
    /// <param name="item">The user name and the Discord account.</param>
    /// <returns>The account.</returns>
    public Task<UserDTO> UserDiscordCreateAsync(UserDiscordCreateRequest item) => PostAsync<UserDTO>("user/discord/create", item);

    /// <summary>Links a Discord account to an account, or unlinks it (no DiscordId).</summary>
    /// <param name="item">The account and the Discord account.</param>
    /// <returns>The account.</returns>
    public Task<UserDTO> UserDiscordLinkAsync(UserDiscordLinkRequest item) => PostAsync<UserDTO>("user/discord", item);

    /// <summary>Changes a password.</summary>
    /// <param name="item">The account, current and new password.</param>
    /// <returns>The account.</returns>
    public Task<UserDTO> UserPasswordAsync(UserPasswordRequest item) => PostAsync<UserDTO>("user/password", item);

    /// <summary>Deletes an account and its data.</summary>
    /// <param name="identifier">The account id.</param>
    /// <returns>A task that completes on success.</returns>
    public Task UserDeleteAsync(string identifier) => DeleteAsync($"user?identifier={Uri.EscapeDataString(identifier)}");

    // ----- Settings -----

    /// <summary>Keeps the language and which optional modules are on.</summary>
    /// <param name="item">The choices (what is left empty stays as it is).</param>
    /// <returns>The user, with the choices as they are now.</returns>
    public Task<UserDTO> SettingSaveAsync(SettingSaveRequest item) => PostAsync<UserDTO>("setting", item);

    // ----- Accounts -----

    /// <summary>Lists a user's accounts with their balances.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The accounts, in the person's order.</returns>
    public Task<List<AccountDTO>> AccountListAsync(string userId) => GetAsync<List<AccountDTO>>($"account/list?userId={Uri.EscapeDataString(userId)}");

    /// <summary>Adds an account, or changes one.</summary>
    /// <param name="item">The account.</param>
    /// <returns>The account.</returns>
    public Task<AccountDTO> AccountSaveAsync(AccountSaveRequest item) => PostAsync<AccountDTO>("account", item);

    /// <summary>Keeps the order the accounts were dragged into.</summary>
    /// <param name="item">The order.</param>
    /// <returns>The accounts in their new order.</returns>
    public Task<List<AccountDTO>> AccountOrderAsync(AccountOrderRequest item) => PostAsync<List<AccountDTO>>("account/order", item);

    /// <summary>Deletes an account; its entries stay in the ledger.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="identifier">The account.</param>
    /// <returns>A task that completes when it is deleted.</returns>
    public Task AccountDeleteAsync(string userId, string identifier) =>
        DeleteAsync($"account?userId={Uri.EscapeDataString(userId)}&identifier={Uri.EscapeDataString(identifier)}");

    // ----- Entries -----

    /// <summary>The entries of a month.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="month">The month (yyyy-MM; null: this month).</param>
    /// <param name="scope">The part of life (null: all).</param>
    /// <param name="category">A category key, or "transfers" (null: all).</param>
    /// <returns>The entries.</returns>
    public Task<EntryMonthDTO> EntryMonthAsync(string userId, string? month = null, EntryScope? scope = null, string? category = null) =>
        GetAsync<EntryMonthDTO>($"entry/month?userId={Uri.EscapeDataString(userId)}{Optional("month", month)}{(scope is { } part ? "&scope=" + part : string.Empty)}{Optional("category", category)}");

    /// <summary>Gets one entry.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="identifier">The entry.</param>
    /// <returns>The entry.</returns>
    public Task<EntryDTO> EntryGetAsync(string userId, string identifier) =>
        GetAsync<EntryDTO>($"entry?userId={Uri.EscapeDataString(userId)}&identifier={Uri.EscapeDataString(identifier)}");

    /// <summary>Reads a quick-add line ("9.80 lunch") and fills in the rest from the last entry with the same words.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="text">The line.</param>
    /// <returns>What was understood.</returns>
    public Task<QuickAddDTO> EntryQuickAsync(string userId, string text) =>
        GetAsync<QuickAddDTO>($"entry/quick?userId={Uri.EscapeDataString(userId)}&text={Uri.EscapeDataString(text)}");

    /// <summary>Adds an entry, or changes one.</summary>
    /// <param name="item">The entry.</param>
    /// <returns>The entry.</returns>
    public Task<EntryDTO> EntrySaveAsync(EntrySaveRequest item) => PostAsync<EntryDTO>("entry", item);

    /// <summary>Deletes an entry.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="identifier">The entry.</param>
    /// <returns>A task that completes when it is deleted.</returns>
    public Task EntryDeleteAsync(string userId, string identifier) =>
        DeleteAsync($"entry?userId={Uri.EscapeDataString(userId)}&identifier={Uri.EscapeDataString(identifier)}");

    // ----- Overview -----

    /// <summary>The overview of a month of the person's own money.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="month">The month (yyyy-MM; null: this month).</param>
    /// <returns>The overview.</returns>
    public Task<OverviewDTO> OverviewGetAsync(string userId, string? month = null) =>
        GetAsync<OverviewDTO>($"overview?userId={Uri.EscapeDataString(userId)}{Optional("month", month)}");

    /// <summary>Sets the spending cap of a month, or removes it.</summary>
    /// <param name="item">The month and the cap.</param>
    /// <returns>The month.</returns>
    public Task<string> OverviewCapSaveAsync(CapSaveRequest item) => PostAsync<string>("overview/cap", item);

    // ----- Short-term rental -----

    /// <summary>A month of the rental: revenue, costs, margin, the booked nights and the year so far.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="month">The month (yyyy-MM; null: this month).</param>
    /// <returns>The month.</returns>
    public Task<RentalDTO> RentalGetAsync(string userId, string? month = null) =>
        GetAsync<RentalDTO>($"rental?userId={Uri.EscapeDataString(userId)}{Optional("month", month)}");

    // ----- Buy & Sell -----

    /// <summary>Every project, worked out from its entries, and what they add up to.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The projects.</returns>
    public Task<ProjectsDTO> ProjectListAsync(string userId) => GetAsync<ProjectsDTO>($"project/list?userId={Uri.EscapeDataString(userId)}");

    /// <summary>Adds a project, or changes one.</summary>
    /// <param name="item">The project.</param>
    /// <returns>The project's id.</returns>
    public Task<string> ProjectSaveAsync(ProjectSaveRequest item) => PostAsync<string>("project", item);

    /// <summary>Deletes a project; its entries stay in the ledger.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="identifier">The project.</param>
    /// <returns>A task that completes when it is deleted.</returns>
    public Task ProjectDeleteAsync(string userId, string identifier) =>
        DeleteAsync($"project?userId={Uri.EscapeDataString(userId)}&identifier={Uri.EscapeDataString(identifier)}");

    // ----- Backup -----

    /// <summary>Every entry as a CSV file.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The file.</returns>
    public Task<ExportDTO> BackupCsvAsync(string userId) => GetAsync<ExportDTO>($"backup/csv?userId={Uri.EscapeDataString(userId)}");

    /// <summary>The whole ledger as a JSON file.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The file.</returns>
    public Task<ExportDTO> BackupJsonAsync(string userId) => GetAsync<ExportDTO>($"backup/json?userId={Uri.EscapeDataString(userId)}");

    /// <summary>Reads a CSV or a JSON backup into the ledger.</summary>
    /// <param name="item">The file, and whether it replaces what is there.</param>
    /// <returns>What was added.</returns>
    public Task<ImportResultDTO> BackupImportAsync(ImportRequest item) => PostAsync<ImportResultDTO>("backup/import", item);

    /// <summary>Start over: erases the ledger and puts the four starting accounts back.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>A task that completes when it is erased.</returns>
    public Task BackupResetAsync(string userId) => DeleteAsync($"backup?userId={Uri.EscapeDataString(userId)}");

    // ----- Health -----

    /// <summary>Checks the WebApi and its database.</summary>
    /// <returns>The health.</returns>
    public Task<HealthDTO> HealthGetAsync() => GetAsync<HealthDTO>("health");

    private static string Optional(string name, string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : $"&{name}={Uri.EscapeDataString(value)}";

    private async Task<T> GetAsync<T>(string path)
    {
        using var response = await httpClient.GetAsync(path);
        return await ReadAsync<T>(response);
    }

    private async Task<T> PostAsync<T>(string path, object? body)
    {
        using var response = await httpClient.PostAsJsonAsync(path, body, SerializerOptions);
        return await ReadAsync<T>(response);
    }

    private async Task DeleteAsync(string path)
    {
        using var response = await httpClient.DeleteAsync(path);
        await EnsureSuccessAsync(response);
    }

    private async Task<T> DeleteAsync<T>(string path)
    {
        using var response = await httpClient.DeleteAsync(path);
        return await ReadAsync<T>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(SerializerOptions))!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ApiValidationProblemDetails? problems = null;
        try
        {
            problems = await response.Content.ReadFromJsonAsync<ApiValidationProblemDetails>(SerializerOptions);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            // Not a problem body (an empty 401, an HTML error page).
        }

        throw new LedgerApiException(response.StatusCode, problems);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
