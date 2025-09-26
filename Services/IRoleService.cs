using Microsoft.AspNetCore.Identity;

namespace Blog.Services
{
    public interface IRoleService
    {
        Task<bool> IsUserInRoleAsync(string userId, string role);
        Task<bool> AddUserToRoleAsync(string userId, string role);
        Task<bool> RemoveUserFromRoleAsync(string userId, string role);
        Task<IList<string>> GetUserRolesAsync(string userId);
        Task<List<IdentityUser>> GetUsersInRoleAsync(string role);
    }
}