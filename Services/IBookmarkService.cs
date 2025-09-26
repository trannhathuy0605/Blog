using Blog.Models;

namespace Blog.Services
{
    public interface IBookmarkService
    {
        Task<bool> BookmarkPostAsync(string userId, int postId);
        Task<bool> RemoveBookmarkAsync(string userId, int postId);
        Task<bool> IsBookmarkedAsync(string userId, int postId);
        Task<List<BlogPost>> GetUserBookmarksAsync(string userId);
        Task<int> GetBookmarkCountAsync(int postId);
    }
}