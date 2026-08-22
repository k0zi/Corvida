using System.Collections.Generic;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

/// <summary>Administrator-only user management against the Corvida API.</summary>
public interface IUserAdminService
{
    Task<List<UserAccount>> GetUsersAsync();
    Task<UserAccount> CreateUserAsync(string email, string password, string displayName, string role);
    Task<UserAccount> SaveUserAsync(UserAccount user);
    Task SetPasswordAsync(string userId, string? currentPassword, string newPassword);
    Task DeleteUserAsync(string userId);
}
