using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Ledger.WebApi.Features.Accounts.Http;

/// <summary>
/// Obol, the investment tracker next door: what a person's portfolio is worth.
/// </summary>
internal interface IObolClient
{
    /// <summary>What a person's portfolio is worth now, cash included.</summary>
    /// <param name="userName">The person's user name in Obol.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The value, or null when Obol has nothing for them yet.</returns>
    Task<decimal?> ValueAsync(string userName, CancellationToken cancellationToken = default);
}

/// <summary>Obol said no, or said something Ledger couldn't read.</summary>
/// <param name="message">What happened.</param>
public sealed class ObolException(string message) : Exception(message);

/// <summary>
/// Calls Obol's WebApi with its API key (GET user/list, then GET portfolio/overview).
/// </summary>
/// <param name="httpClientFactory">Makes the HTTP clients (named after this class).</param>
/// <param name="options">Obol's address and key.</param>
internal sealed class ObolClient(IHttpClientFactory httpClientFactory, IOptions<LedgerOptions> options) : IObolClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly LedgerOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<decimal?> ValueAsync(string userName, CancellationToken cancellationToken = default)
    {
        if (!_options.ObolEnabled)
        {
            throw new ObolException("OBOL_URL / OBOL_API_KEY aren't set.");
        }

        var users = await GetAsync<List<ObolUser>>("user/list", cancellationToken);
        var user = users.FirstOrDefault(row => string.Equals(row.UserName, userName.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ObolException($"no user \"{userName}\" in Obol");

        var overview = await GetAsync<ObolOverview>($"portfolio/overview?userId={Uri.EscapeDataString(user.Id)}&range=Week", cancellationToken);
        return overview.HasData ? decimal.Round(overview.TotalValue, 2, MidpointRounding.AwayFromZero) : null;
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_options.ObolUrl.TrimEnd('/') + "/"), path));
            request.Headers.Add("x-api-key", _options.ObolApiKey);
            using var response = await httpClientFactory.CreateClient(nameof(ObolClient)).SendAsync(request, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new ObolException($"{(int)response.StatusCode} {(text.Length > 160 ? text[..160] : text)}");
            }

            return JsonSerializer.Deserialize<T>(text, Json) ?? throw new ObolException("empty answer");
        }
        catch (HttpRequestException exception)
        {
            throw new ObolException(exception.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ObolException("timeout");
        }
        catch (JsonException exception)
        {
            throw new ObolException($"unreadable answer ({exception.Message})");
        }
    }

    private sealed class ObolUser
    {
        public string Id { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;
    }

    private sealed class ObolOverview
    {
        public bool HasData { get; set; }

        public decimal TotalValue { get; set; }
    }
}
