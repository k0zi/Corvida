using Microsoft.AspNetCore.Identity;

namespace Corvida.Api.Auth;

public interface IRoleSeeder
{
    Task EnsureRolesAsync(CancellationToken ct = default);
}

/// <summary>Creates the fixed Admin/User roles so role assignment never has to create them ad hoc.</summary>
internal sealed class RoleSeeder(RoleManager<IdentityRole> roles, ILogger<RoleSeeder> logger) : IRoleSeeder
{
    public async Task EnsureRolesAsync(CancellationToken ct = default)
    {
        foreach (var role in Roles.All)
        {
            if (await roles.RoleExistsAsync(role).ConfigureAwait(false)) continue;

            var result = await roles.CreateAsync(new IdentityRole(role)).ConfigureAwait(false);
            if (result.Succeeded)
                logger.LogInformation("Created role {Role}", role);
            else
                logger.LogError("Could not create role {Role}: {Errors}", role,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
