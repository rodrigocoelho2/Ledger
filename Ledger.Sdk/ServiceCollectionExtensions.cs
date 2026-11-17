using Ledger.Library.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ledger.Sdk;

/// <summary>
/// Where the WebApi is and the key to call it, bound from the "LedgerApi" section.
/// </summary>
public sealed class LedgerClientOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "LedgerApi";

    /// <summary>Gets or sets the WebApi base URL, e.g. http://webapi:8080/.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets the key sent in x-api-key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the request timeout (a big CSV import can take a while).</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);
}

/// <summary>
/// Registers <see cref="LedgerClient"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the typed client with its base address and x-api-key header.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration holding the "LedgerApi" section.</param>
    /// <param name="configure">Adds to the client's pipeline (the Website adds the handler that sends the person's language).</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLedgerClient(this IServiceCollection services, IConfiguration configuration, Action<IHttpClientBuilder>? configure = null)
    {
        var options = configuration.GetSection(LedgerClientOptions.SectionName).Get<LedgerClientOptions>() ?? new LedgerClientOptions();
        if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var baseAddress))
        {
            throw new InvalidOperationException("LedgerApi:Url must be an absolute URL (env LedgerApi__Url).");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("LedgerApi:ApiKey is required (env LedgerApi__ApiKey).");
        }

        // A trailing slash makes relative paths ("entry/month") append to the base path instead of replacing it.
        var normalizedBase = baseAddress.AbsoluteUri.EndsWith('/') ? baseAddress : new Uri(baseAddress.AbsoluteUri + "/");

        var builder = services.AddHttpClient<LedgerClient>(client =>
        {
            client.BaseAddress = normalizedBase;
            client.Timeout = options.Timeout;
            client.DefaultRequestHeaders.Add(ApiHeaders.ApiKey, options.ApiKey);
        });
        configure?.Invoke(builder);

        return services;
    }
}
