using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Server;
using OpenIddict.Validation.AspNetCore;

namespace Corvida.Api.Auth;

public static class AuthServiceCollectionExtensions
{
    /// <summary>
    /// When true, OpenIddict accepts requests over plain HTTP. Required by the default
    /// docker-compose deployment, which serves HTTP on :5083 in the Production environment.
    /// </summary>
    public static bool AllowsInsecureTransport(this IConfiguration configuration) =>
        configuration.GetValue("Auth:AllowInsecureTransport", defaultValue: true);

    public static IServiceCollection AddCorvidaAuth(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("CorvidaApi");

        services.AddDbContext<AuthDbContext>(opt =>
            opt.UseNpgsql(connectionString, npgsql => npgsql
                .EnableRetryOnFailure()
                .MigrationsHistoryTable(AuthSchema.MigrationsHistoryTable, AuthSchema.Name)));
        // NOTE: no .UseOpenIddict() here on purpose — it is applied in AuthDbContext.OnModelCreating
        // so the API test fixture cannot drop it when it repoints the context.

        services.TryAddSingletonTimeProvider();

        services.AddSingleton<ISigningKeyProvider, SigningKeyProvider>();
        services.AddSingleton<IConfigureOptions<OpenIddictServerOptions>, SigningKeyServerConfiguration>();

        services.AddOpenIddict()

            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                       .UseDbContext<AuthDbContext>();

                // Disabling auth TRUNCATEs the OpenIddict tables out from under the managers, and
                // their scoped entity cache has no public invalidation hook — a cached token could
                // otherwise outlive the wipe.
                options.DisableEntityCaching();
            })

            .AddServer(options =>
            {
                options.SetTokenEndpointUris("connect/token");

                // AllowRefreshTokenFlow also registers the offline_access scope.
                options.AllowPasswordFlow()
                       .AllowRefreshTokenFlow();

                // Corvida has one first-party client family and no client registry, so token
                // requests carry no client_id.
                options.AcceptAnonymousClients();

                // Access tokens stay readable JWTs so a 401 is diagnosable; refresh tokens remain
                // encrypted either way, and encryption credentials are required regardless.
                options.DisableAccessTokenEncryption();

                options.SetAccessTokenLifetime(TimeSpan.FromMinutes(30))
                       .SetRefreshTokenLifetime(TimeSpan.FromDays(30));

                // Signing/encryption credentials come from SigningKeyServerConfiguration.

                var aspNetCore = options.UseAspNetCore()
                    .EnableTokenEndpointPassthrough();

                // The default deployment serves plain HTTP on :5083 in Production; without this
                // OpenIddict refuses every request. Set Auth:AllowInsecureTransport=false once the
                // API is behind TLS.
                if (configuration.AllowsInsecureTransport())
                    aspNetCore.DisableTransportSecurityRequirement();
            })

            .AddValidation(options =>
            {
                options.UseLocalServer();   // imports issuer + credentials from the in-process server
                options.UseAspNetCore();
                // Reading the token from the "access_token" query string is on by default, which is
                // what SignalR's WebSocket transport needs. Do not disable it.
            });

        services.AddAuthentication(CorvidaSchemes.Dispatch)
            .AddPolicyScheme(CorvidaSchemes.Dispatch, displayName: null, options =>
                // Must always resolve to a concrete, non-self scheme: PolicySchemeHandler throws
                // for any operation it is not forwarding.
                options.ForwardDefaultSelector = static context =>
                    PersonalAccessTokens.IsPresented(context)
                        ? CorvidaSchemes.PersonalAccessToken
                        : OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, PatAuthenticationHandler>(
                CorvidaSchemes.PersonalAccessToken, displayName: null, configureOptions: _ => { });

        services.AddIdentityCore<CorvidaUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 8;
                o.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddSignInManager();
        // Deliberately no AddDefaultTokenProviders(): the email-confirmation / reset-token providers
        // are unused (password resets are admin-driven) and would pull in DataProtection, whose
        // keyring is ephemeral in the API container. Consequences, which the code must respect:
        //   - use CheckPasswordSignInAsync, never PasswordSignInAsync (two-factor ticket path);
        //   - use ChangePasswordAsync / RemovePassword+AddPassword, never Generate*TokenAsync.

        services.AddScoped<IRoleSeeder, RoleSeeder>();
        services.AddSingleton<IHubConnectionRegistry, HubConnectionRegistry>();

        services.AddSingleton<IAuthGateState, AuthGateState>();
        services.AddSingleton<IAuthorizationHandler, CorvidaAccessHandler>();

        // Every policy names the scheme explicitly. PolicyEvaluator falls back to the default
        // authenticate scheme when a policy lists none, so leaving them empty would silently change
        // meaning if DefaultScheme is ever repointed.
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.User, p => p
                .AddAuthenticationSchemes(CorvidaSchemes.Dispatch)
                .AddRequirements(new CorvidaAccessRequirement()))
            .AddPolicy(Policies.Admin, p => p
                .AddAuthenticationSchemes(CorvidaSchemes.Dispatch)
                .AddRequirements(new CorvidaAccessRequirement(Roles.Admin)))
            // Defence in depth: an endpoint added later without explicit metadata is gated by
            // default, and .AllowAnonymous() is the only way out.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(CorvidaSchemes.Dispatch)
                .AddRequirements(new CorvidaAccessRequirement())
                .Build());

        return services;
    }


    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(TimeProvider))) return;
        services.AddSingleton(TimeProvider.System);
    }
}
