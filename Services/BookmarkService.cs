using Blog.Data;
using Blog.Models;
using Microsoft.EntityFrameworkCore;

namespace Blog.Services
{
    public class BookmarkService : IBookmarkService
    {
        private readonly ApplicationDbContext _context;

        public BookmarkService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> BookmarkPostAsync(string userId, int postId)
        {
            try
            {
                // Kiểm tra xem đã bookmark chưa
                var existingBookmark = await _context.Bookmarks
                    .FirstOrDefaultAsync(b => b.UserId == userId && b.BlogPostId == postId);

                if (existingBookmark != null)
                    return false; // Đã bookmark rồi

                var bookmark = new Bookmark
                {
                    UserId = userId,
                    BlogPostId = postId,
                    CreatedAt = DateTime.Now
                };

                _context.Bookmarks.Add(bookmark);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RemoveBookmarkAsync(string userId, int postId)
        {
            try
            {
                var bookmark = await _context.Bookmarks
                    .FirstOrDefaultAsync(b => b.UserId == userId && b.BlogPostId == postId);

                if (bookmark == null)
                    return false;

                _context.Bookmarks.Remove(bookmark);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> IsBookmarkedAsync(string userId, int postId)
        {
            return await _context.Bookmarks
                .AnyAsync(b => b.UserId == userId && b.BlogPostId == postId);
        }

        public async Task<List<BlogPost>> GetUserBookmarksAsync(string userId)
        {
            return await _context.Bookmarks
                .Where(b => b.UserId == userId)
                .Include(b => b.BlogPost)
                .Select(b => b.BlogPost)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<int> GetBookmarkCountAsync(int postId)
        {
            return await _context.Bookmarks
                .CountAsync(b => b.BlogPostId == postId);
        }
    }
}