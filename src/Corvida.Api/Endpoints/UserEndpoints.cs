using Corvida.Api.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Corvida.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/users").RequireAuthorization(Policies.Admin);

        g.MapGet("/", GetUsers);
        g.MapPost("/", CreateUser);
        g.MapPut("/{id}", UpdateUser);
        g.MapPost("/{id}/password", SetPassword);
        g.MapDelete("/{id}", DeleteUser);
    }

    private static async Task<IResult> GetUsers(UserManager<CorvidaUser> users)
    {
        var all = await users.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync();

        var result = new List<UserResponse>(all.Count);
        foreach (var user in all)
            result.Add(await ToResponseAsync(users, user));

        return Results.Ok(result);
    }

    private static async Task<IResult> CreateUser(CreateUserRequest request, UserManager<CorvidaUser> users)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
            return Results.BadRequest(new { Error = "Email and Password are required." });

        var role = NormalizeRole(request.Role);
        if (role is null)
            return Results.BadRequest(new { Error = $"Role must be one of: {string.Join(", ", Roles.All)}." });

        var user = new CorvidaUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? request.Email.Trim()
                : request.DisplayName.Trim(),
        };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded) return Failed(created);

        var assigned = await users.AddToRoleAsync(user, role);
        if (!assigned.Succeeded)
        {
            await users.DeleteAsync(user);
            return Failed(assigned);
        }

        return Results.Created($"/api/users/{user.Id}", await ToResponseAsync(users, user));
    }

    private static async Task<IResult> UpdateUser(
        string id, UpdateUserRequest request, UserManager<CorvidaUser> users)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return Results.NotFound();

        var role = NormalizeRole(request.Role);
        if (role is null)
            return Results.BadRequest(new { Error = $"Role must be one of: {string.Join(", ", Roles.All)}." });

        // Demoting the last administrator would lock everyone out of user management, with no way
        // back except editing the database by hand.
        if (role != Roles.Admin && await IsLastAdminAsync(users, user))
            return Results.Conflict(new { Error = "This is the only administrator." });

        user.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? user.UserName ?? string.Empty
            : request.DisplayName.Trim();

        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded) return Failed(updated);

        var current = await users.GetRolesAsync(user);
        if (!current.Contains(role))
        {
            await users.RemoveFromRolesAsync(user, current);
            var assigned = await users.AddToRoleAsync(user, role);
            if (!assigned.Succeeded) return Failed(assigned);
        }

        return Results.Ok(await ToResponseAsync(users, user));
    }

    private static async Task<IResult> SetPassword(
        string id, SetPasswordRequest request, UserManager<CorvidaUser> users, HttpContext http)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return Results.NotFound();

        if (string.IsNullOrEmpty(request.NewPassword))
            return Results.BadRequest(new { Error = "NewPassword is required." });

        var callerId = http.User.FindFirst(Claims.Subject)?.Value;

        // An administrator resetting someone else's password has no old password to supply, so the
        // reset is remove-then-add. Token-based resets are not available: no token providers are
        // registered, deliberately.
        if (callerId != user.Id)
        {
            var removed = await users.RemovePasswordAsync(user);
            if (!removed.Succeeded) return Failed(removed);

            var added = await users.AddPasswordAsync(user, request.NewPassword);
            return added.Succeeded ? Results.NoContent() : Failed(added);
        }

        if (string.IsNullOrEmpty(request.CurrentPassword))
            return Results.BadRequest(new { Error = "CurrentPassword is required to change your own password." });

        var changed = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        return changed.Succeeded ? Results.NoContent() : Failed(changed);
    }

    private static async Task<IResult> DeleteUser(
        string id, UserManager<CorvidaUser> users, HttpContext http)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return Results.NotFound();

        if (http.User.FindFirst(Claims.Subject)?.Value == user.Id)
            return Results.Conflict(new { Error = "You cannot delete your own account." });

        if (await IsLastAdminAsync(users, user))
            return Results.Conflict(new { Error = "This is the only administrator." });

        var deleted = await users.DeleteAsync(user);
        return deleted.Succeeded ? Results.NoContent() : Failed(deleted);
    }

    private static async Task<bool> IsLastAdminAsync(UserManager<CorvidaUser> users, CorvidaUser user)
    {
        if (!await users.IsInRoleAsync(user, Roles.Admin)) return false;

        var admins = await users.GetUsersInRoleAsync(Roles.Admin);
        return admins.Count <= 1;
    }

    private static string? NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return Roles.User;

        return Roles.All.FirstOrDefault(r => string.Equals(r, role.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<UserResponse> ToResponseAsync(UserManager<CorvidaUser> users, CorvidaUser user)
    {
        var roles = await users.GetRolesAsync(user);

        return new UserResponse(
            user.Id,
            user.UserName ?? string.Empty,
            user.Email ?? string.Empty,
            user.DisplayName,
            roles.Contains(Roles.Admin) ? Roles.Admin : Roles.User,
            user.LockoutEnd is not null && user.LockoutEnd > DateTimeOffset.UtcNow);
    }

    private static IResult Failed(IdentityResult result) =>
        Results.BadRequest(new
        {
            Error = "The request was rejected.",
            Details = result.Errors.Select(e => e.Description).ToArray(),
        });
}

public record UserResponse(
    string Id, string UserName, string Email, string DisplayName, string Role, bool IsLockedOut);

public record CreateUserRequest(string Email, string Password, string? DisplayName, string? Role);

public record UpdateUserRequest(string? DisplayName, string? Role);

public record SetPasswordRequest(string? CurrentPassword, string NewPassword);
