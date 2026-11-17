using System.Text.Json.Serialization;
using Ledger.WebApi.Extensions.Authentication;
using Ledger.WebApi.Extensions.Data;
using Ledger.WebApi.Extensions.Globalization;
using Ledger.WebApi.Extensions.Mvc;
using Ledger.WebApi.Extensions.Time;
using Ledger.WebApi.Features;
using Ledger.WebApi.Features.Accounts.Repositories;
using Ledger.WebApi.Features.Accounts.Services;
using Ledger.WebApi.Features.Backup.Services;
using Ledger.WebApi.Features.Demo.Services;
using Ledger.WebApi.Features.Entries.Repositories;
using Ledger.WebApi.Features.Entries.Services;
using Ledger.WebApi.Features.Overview.Repositories;
using Ledger.WebApi.Features.Overview.Services;
using Ledger.WebApi.Features.Projects.Repositories;
using Ledger.WebApi.Features.Projects.Services;
using Ledger.WebApi.Features.Rental.Services;
using Ledger.WebApi.Features.Settings.Repositories;
using Ledger.WebApi.Features.Settings.Services;
using Ledger.WebApi.Features.Users.Repositories;
using Ledger.WebApi.Features.Users.Services;
using Microsoft.AspNetCore.Authentication;

namespace Ledger.WebApi;

/// <summary>
/// Service registration and request pipeline of the WebApi.
/// </summary>
internal static class LedgerStartup
{
    /// <summary>
    /// Registers every service. All services and repositories are singletons; each repository is registered before
    /// its service. In demo mode the repositories keep everything in memory and no SQL Server is needed.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLedger(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var section = configuration.GetSection(LedgerOptions.SectionName);
        var demoMode = section.GetValue<bool>(nameof(LedgerOptions.DemoMode));
        services.AddOptions<LedgerOptions>()
            .Bind(section)
            .Validate(static options => !string.IsNullOrWhiteSpace(options.ApiKey), "Ledger:ApiKey is required (env Ledger__ApiKey, LEDGER_API_KEY in .env).")
            .ValidateOnStart();

        services.AddControllers()
            .ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add(new InternalControllerFeatureProvider()))
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);
        services.AddAuthorization();

        if (environment.IsDevelopment())
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options => options.EnableAnnotations());
        }

        // Infrastructure
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddSingleton<IAppClock, AppClock>();
        services.AddSingleton<IGlobalizationHelper, GlobalizationHelper>();

        // Repositories: SQL Server, or memory in demo mode.
        if (demoMode)
        {
            services.AddSingleton<MemoryStore>();
            services.AddSingleton<IUserRepository, MemoryUserRepository>();
            services.AddSingleton<ISettingRepository, MemorySettingRepository>();
            services.AddSingleton<IAccountRepository, MemoryAccountRepository>();
            services.AddSingleton<IProjectRepository, MemoryProjectRepository>();
            services.AddSingleton<IEntryRepository, MemoryEntryRepository>();
            services.AddSingleton<ICapRepository, MemoryCapRepository>();
        }
        else
        {
            services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
            services.AddSingleton<IUserRepository, UserRepository>();
            services.AddSingleton<ISettingRepository, SettingRepository>();
            services.AddSingleton<IAccountRepository, AccountRepository>();
            services.AddSingleton<IProjectRepository, ProjectRepository>();
            services.AddSingleton<IEntryRepository, EntryRepository>();
            services.AddSingleton<ICapRepository, CapRepository>();
        }

        // What a new user starts with: four empty accounts, or the sample ledger in demo mode.
        if (demoMode)
        {
            services.AddSingleton<ILedgerSeeder, DemoSeeder>();
            services.AddHostedService<DemoStartup>();
        }
        else
        {
            services.AddSingleton<ILedgerSeeder, StarterSeeder>();
        }

        // Services
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<ISettingService, SettingService>();
        services.AddSingleton<IAccountService, AccountService>();
        services.AddSingleton<IEntryService, EntryService>();
        services.AddSingleton<IOverviewService, OverviewService>();
        services.AddSingleton<IRentalService, RentalService>();
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IBackupService, BackupService>();

        return services;
    }

    /// <summary>
    /// Configures the request pipeline.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The application.</returns>
    public static WebApplication UseLedger(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        return app;
    }
}
