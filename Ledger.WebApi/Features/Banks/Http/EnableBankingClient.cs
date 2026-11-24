using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Ledger.WebApi.Features.Banks.Http;

/// <summary>
/// Enable Banking's API: the person's own accounts, read-only, through PSD2.
/// </summary>
internal interface IEnableBankingClient
{
    /// <summary>The banks of a country.</summary>
    /// <param name="country">The country (PT).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The banks.</returns>
    Task<List<EbAspsp>> BanksAsync(string country, CancellationToken cancellationToken = default);

    /// <summary>The addresses registered for the application.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The redirect addresses.</returns>
    Task<List<string>> RedirectUrlsAsync(CancellationToken cancellationToken = default);

    /// <summary>Starts an authorisation at a bank.</summary>
    /// <param name="bank">The bank's name.</param>
    /// <param name="country">The bank's country.</param>
    /// <param name="validUntil">Until when the consent is asked for.</param>
    /// <param name="redirectUrl">Where the bank sends the person back.</param>
    /// <param name="state">What comes back with the person.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Where the person goes.</returns>
    Task<string> StartAsync(string bank, string country, DateTimeOffset validUntil, string redirectUrl, string state, CancellationToken cancellationToken = default);

    /// <summary>Turns the code the bank sent back into a session.</summary>
    /// <param name="code">The code.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The session and its accounts.</returns>
    Task<EbSession> SessionAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Ends a session at the bank (best effort).</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when it is ended.</returns>
    Task EndAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>The balances of an account.</summary>
    /// <param name="accountUid">The account.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The balances.</returns>
    Task<List<EbBalance>> BalancesAsync(string accountUid, CancellationToken cancellationToken = default);

    /// <summary>The booked transactions of an account between two days, every page.</summary>
    /// <param name="accountUid">The account.</param>
    /// <param name="from">The first day.</param>
    /// <param name="to">The last day.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The transactions.</returns>
    Task<List<EbTransaction>> TransactionsAsync(string accountUid, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

/// <summary>
/// Calls Enable Banking with a short-lived JWT signed by the application's private key (RS256, kid = application id).
/// </summary>
/// <param name="httpClientFactory">Makes the HTTP clients (named after this class).</param>
/// <param name="options">The app id and key.</param>
/// <param name="timeProvider">The clock, for the token.</param>
internal sealed class EnableBankingClient(IHttpClientFactory httpClientFactory, IOptions<LedgerOptions> options, TimeProvider timeProvider) : IEnableBankingClient
{
    /// <summary>How long a token lives.</summary>
    public static readonly TimeSpan TokenLife = TimeSpan.FromHours(1);

    /// <summary>The most pages of transactions read in one go (a bank repeating its key would loop forever).</summary>
    public const int MaxPages = 50;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private readonly LedgerOptions _options = options.Value;
    private readonly Lock _gate = new();
    private string? _token;
    private DateTimeOffset _tokenEnds;

    /// <inheritdoc />
    public async Task<List<EbAspsp>> BanksAsync(string country, CancellationToken cancellationToken = default) =>
        (await SendAsync<EbAspsps>(HttpMethod.Get, $"aspsps?country={Uri.EscapeDataString(country)}&psu_type=personal", null, cancellationToken)).Aspsps;

    /// <inheritdoc />
    public async Task<List<string>> RedirectUrlsAsync(CancellationToken cancellationToken = default) =>
        (await SendAsync<EbApplication>(HttpMethod.Get, "application", null, cancellationToken)).RedirectUrls;

    /// <inheritdoc />
    public async Task<string> StartAsync(string bank, string country, DateTimeOffset validUntil, string redirectUrl, string state, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            access = new { valid_until = validUntil.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture), balances = true, transactions = true },
            aspsp = new { name = bank, country },
            state,
            redirect_url = redirectUrl,
            psu_type = "personal",
        };
        return (await SendAsync<EbAuth>(HttpMethod.Post, "auth", body, cancellationToken)).Url;
    }

    /// <inheritdoc />
    public Task<EbSession> SessionAsync(string code, CancellationToken cancellationToken = default) =>
        SendAsync<EbSession>(HttpMethod.Post, "sessions", new { code }, cancellationToken);

    /// <inheritdoc />
    public async Task EndAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = Request(HttpMethod.Delete, $"sessions/{Uri.EscapeDataString(sessionId)}", null);
            using var response = await httpClientFactory.CreateClient(nameof(EnableBankingClient)).SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // The link goes anyway; the consent ends on its own.
        }
    }

    /// <inheritdoc />
    public async Task<List<EbBalance>> BalancesAsync(string accountUid, CancellationToken cancellationToken = default) =>
        (await SendAsync<EbBalances>(HttpMethod.Get, $"accounts/{Uri.EscapeDataString(accountUid)}/balances", null, cancellationToken)).Balances;

    /// <inheritdoc />
    public async Task<List<EbTransaction>> TransactionsAsync(string accountUid, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var all = new List<EbTransaction>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string? key = null;
        for (var page = 0; page < MaxPages; page++)
        {
            var path = $"accounts/{Uri.EscapeDataString(accountUid)}/transactions?date_from={from:yyyy-MM-dd}&date_to={to:yyyy-MM-dd}&transaction_status=BOOK"
                + (key is null ? string.Empty : $"&continuation_key={Uri.EscapeDataString(key)}");
            var result = await SendAsync<EbTransactions>(HttpMethod.Get, path, null, cancellationToken);
            all.AddRange(result.Transactions);
            key = result.ContinuationKey;
            if (string.IsNullOrEmpty(key) || !seen.Add(key))
            {
                break;
            }
        }

        return all;
    }

    /// <summary>Builds the token Enable Banking wants: RS256, kid = application id, a life of at most a day.</summary>
    /// <param name="appId">The application id.</param>
    /// <param name="key">The private key (PEM, or PEM in base64).</param>
    /// <param name="now">Now.</param>
    /// <returns>The token.</returns>
    internal static string Token(string appId, string key, DateTimeOffset now)
    {
        var header = new Dictionary<string, string> { ["typ"] = "JWT", ["alg"] = "RS256", ["kid"] = appId };
        var claims = new Dictionary<string, object>
        {
            ["iss"] = "enablebanking.com",
            ["aud"] = "api.enablebanking.com",
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.Add(TokenLife).ToUnixTimeSeconds(),
        };
        var unsigned = $"{Base64Url(JsonSerializer.SerializeToUtf8Bytes(header))}.{Base64Url(JsonSerializer.SerializeToUtf8Bytes(claims))}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(Pem(key));
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{unsigned}.{Base64Url(signature)}";
    }

    /// <summary>Reads the key as it may come from an .env: the PEM itself, with "\n" written out, or in base64.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The PEM.</returns>
    internal static string Pem(string key)
    {
        var text = key.Trim().Trim('"');
        if (text.Contains("-----BEGIN", StringComparison.Ordinal))
        {
            return text.Replace("\\n", "\n", StringComparison.Ordinal);
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(text));
        }
        catch (FormatException)
        {
            throw new BankException("ENABLEBANKING_KEY is neither a PEM nor a PEM in base64.");
        }
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private string Bearer()
    {
        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            if (_token is null || now >= _tokenEnds)
            {
                try
                {
                    _token = Token(_options.EnableBankingAppId.Trim(), _options.EnableBankingKey, now);
                }
                catch (CryptographicException exception)
                {
                    throw new BankException($"ENABLEBANKING_KEY can't be read: {exception.Message}");
                }
                catch (ArgumentException exception)
                {
                    throw new BankException($"ENABLEBANKING_KEY can't be read: {exception.Message}");
                }

                _tokenEnds = now.Add(TokenLife - TimeSpan.FromMinutes(5));
            }

            return _token;
        }
    }

    private HttpRequestMessage Request(HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, new Uri(new Uri(_options.EnableBankingUrl.TrimEnd('/') + "/"), path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Bearer());
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        if (!_options.BankEnabled)
        {
            throw new BankException("ENABLEBANKING_APP_ID / ENABLEBANKING_KEY aren't set.");
        }

        try
        {
            using var request = Request(method, path, body);
            using var response = await httpClientFactory.CreateClient(nameof(EnableBankingClient)).SendAsync(request, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new BankException($"{(int)response.StatusCode} {Problem(text)}");
            }

            return JsonSerializer.Deserialize<T>(text, Json) ?? throw new BankException("empty answer");
        }
        catch (HttpRequestException exception)
        {
            throw new BankException(exception.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BankException("timeout");
        }
        catch (JsonException exception)
        {
            throw new BankException($"unreadable answer ({exception.Message})");
        }
    }

    /// <summary>The useful part of an error body ({"message": …, "detail": …}), kept short.</summary>
    /// <param name="text">The body.</param>
    /// <returns>The message.</returns>
    private static string Problem(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            var parts = new[] { "message", "error", "detail", "code" }
                .Select(name => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) ? value.ToString() : null)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct()
                .ToList();
            text = parts.Count > 0 ? string.Join(" — ", parts) : text;
        }
        catch (JsonException)
        {
            // Not JSON: the text as it came.
        }

        return text.Length > 200 ? text[..200] : text;
    }
}
