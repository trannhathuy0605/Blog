using Blog.Data;
using Blog.Models;
using Blog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Blog.Controllers
{
    public class BlogController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IImageService _imageService;
        private readonly IBookmarkService _bookmarkService;
        private readonly UserManager<IdentityUser> _userManager;

        public BlogController(
            ApplicationDbContext context,
            IImageService imageService,
            IBookmarkService bookmarkService,
            UserManager<IdentityUser> userManager)
        {
            _context = context;
            _imageService = imageService;
            _bookmarkService = bookmarkService;
            _userManager = userManager;
        }

        // GET: Blog
        public async Task<IActionResult> Index(string? category = null, string? tag = null, string? search = null)
        {
            var query = _context.BlogPosts.AsQueryable();

            // Admin có thể xem tất cả bài viết (bao gồm draft), user chỉ xem published
            if (!User.IsInRole("Admin"))
            {
                query = query.Where(p => p.IsPublished);
            }

            // Lọc theo category
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(p => p.Category == category);
            }

            // Tìm kiếm
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p =>
                    p.Title.Contains(search) ||
                    p.Content.Contains(search) ||
                    p.Summary.Contains(search));
            }

            var allPosts = await query
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            // Lọc theo tag (thực hiện ở memory vì Tags được lưu dưới dạng string)
            if (!string.IsNullOrEmpty(tag))
            {
                allPosts = allPosts
                    .Where(p => p.Tags?.Contains(tag) == true)
                    .ToList();
            }

            ViewBag.SearchTerm = search;
            ViewBag.Categories = await _context.BlogPosts
                .Where(p => p.IsPublished && !string.IsNullOrEmpty(p.Category))
                .Select(p => p.Category)
                .Distinct()
                .ToListAsync();

            return View(allPosts);
        }

        // GET: Blog/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var post = await _context.BlogPosts.FirstOrDefaultAsync(p => p.Id == id);
            if (post == null)
            {
                return NotFound();
            }

            // Kiểm tra quyền xem draft posts
            if (!post.IsPublished && !User.IsInRole("Admin"))
            {
                return NotFound();
            }

            // Tăng view count
            post.ViewCount++;
            await _context.SaveChangesAsync();

            // Truyền thông tin bookmark cho view
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    ViewBag.IsBookmarked = await _bookmarkService.IsBookmarkedAsync(userId, id);
                }
            }

            ViewBag.BookmarkCount = await _bookmarkService.GetBookmarkCountAsync(id);

            return View(post);
        }

        // GET: Blog/MyBookmarks
        [Authorize]
        public async Task<IActionResult> MyBookmarks()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var bookmarkedPosts = await _bookmarkService.GetUserBookmarksAsync(userId);
            return View(bookmarkedPosts);
        }

        // ===== NEW ADMIN DRAFT MANAGEMENT =====

        // GET: Blog/DraftPosts - Quản lý bài đăng nháp cho Admin
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DraftPosts(string? search = null, string? category = null, string? author = null, string? sortBy = "newest")
        {
            var query = _context.BlogPosts.Where(p => !p.IsPublished);

            // Tìm kiếm
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p =>
                    p.Title.Contains(search) ||
                    p.Content.Contains(search) ||
                    p.Summary.Contains(search) ||
                    p.Author.Contains(search));
            }

            // Lọc theo category
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(p => p.Category == category);
            }

            // Lọc theo author
            if (!string.IsNullOrEmpty(author))
            {
                query = query.Where(p => p.Author.Contains(author));
            }

            // Sắp xếp
            query = sortBy switch
            {
                "oldest" => query.OrderBy(p => p.CreatedAt),
                "updated" => query.OrderByDescending(p => p.UpdatedAt),
                "title" => query.OrderBy(p => p.Title),
                "author" => query.OrderBy(p => p.Author),
                _ => query.OrderByDescending(p => p.CreatedAt), // newest (default)
            };

            var draftPosts = await query.ToListAsync();

            // Lấy danh sách categories và authors cho filter
            ViewBag.Categories = await _context.BlogPosts
                .Where(p => !p.IsPublished && !string.IsNullOrEmpty(p.Category))
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewBag.Authors = await _context.BlogPosts
                .Where(p => !p.IsPublished && !string.IsNullOrEmpty(p.Author))
                .Select(p => p.Author)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();

            ViewBag.SearchTerm = search;
            ViewBag.SelectedCategory = category;
            ViewBag.SelectedAuthor = author;
            ViewBag.SortBy = sortBy;

            // Thống kê
            ViewBag.TotalDrafts = draftPosts.Count;
            ViewBag.TotalPublished = await _context.BlogPosts.CountAsync(p => p.IsPublished);
            ViewBag.TotalPosts = await _context.BlogPosts.CountAsync();

            return View(draftPosts);
        }

        // POST: Blog/PublishDraft - Xuất bản draft
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PublishDraft(int id)
        {
            try
            {
                var post = await _context.BlogPosts.FindAsync(id);
                if (post == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy bài viết!";
                    return RedirectToAction(nameof(DraftPosts));
                }

                if (post.IsPublished)
                {
                    TempData["InfoMessage"] = "Bài viết đã được xuất bản trước đó!";
                    return RedirectToAction(nameof(DraftPosts));
                }

                post.IsPublished = true;
                post.UpdatedAt = DateTime.Now;

                _context.Update(post);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Đã xuất bản bài viết '{post.Title}' thành công!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi xuất bản bài viết: {ex.Message}";
            }

            return RedirectToAction(nameof(DraftPosts));
        }

        // POST: Blog/BatchPublish - Xuất bản nhiều draft cùng lúc
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BatchPublish([FromBody] List<int> postIds)
        {
            try
            {
                var posts = await _context.BlogPosts
                    .Where(p => postIds.Contains(p.Id) && !p.IsPublished)
                    .ToListAsync();

                if (!posts.Any())
                {
                    return Json(new { success = false, message = "Không tìm thấy bài nháp nào để xuất bản" });
                }

                foreach (var post in posts)
                {
                    post.IsPublished = true;
                    post.UpdatedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Đã xuất bản {posts.Count} bài viết thành công!",
                    publishedCount = posts.Count
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // POST: Blog/BatchDelete - Xóa nhiều draft cùng lúc
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BatchDelete([FromBody] List<int> postIds)
        {
            try
            {
                var posts = await _context.BlogPosts
                    .Where(p => postIds.Contains(p.Id) && !p.IsPublished)
                    .ToListAsync();

                if (!posts.Any())
                {
                    return Json(new { success = false, message = "Không tìm thấy bài nháp nào để xóa" });
                }

                _context.BlogPosts.RemoveRange(posts);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Đã xóa {posts.Count} bài nháp thành công!",
                    deletedCount = posts.Count
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // GET: Blog/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            var model = new BlogPost
            {
                Author = User.Identity?.Name ?? "Unknown",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            return View(model);
        }

        // POST: Blog/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(BlogPost blogPost, IFormFile? FeaturedImageFile, string? TagsInput)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Xử lý upload ảnh đại diện
                    if (FeaturedImageFile != null && FeaturedImageFile.Length > 0)
                    {
                        var imageUrl = await _imageService.UploadImageAsync(FeaturedImageFile, "blog-posts");
                        blogPost.FeaturedImage = imageUrl;
                    }

                    // Xử lý tags
                    if (!string.IsNullOrEmpty(TagsInput))
                    {
                        blogPost.Tags = TagsInput.Split(',')
                            .Select(t => t.Trim())
                            .Where(t => !string.IsNullOrEmpty(t))
                            .ToList();
                    }

                    blogPost.CreatedAt = DateTime.Now;
                    blogPost.UpdatedAt = DateTime.Now;
                    blogPost.Author = User.Identity?.Name ?? "Unknown";

                    _context.BlogPosts.Add(blogPost);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Bài viết đã được tạo thành công!";
                    return RedirectToAction(nameof(Details), new { id = blogPost.Id });
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Lỗi khi tạo bài viết: {ex.Message}";
                }
            }

            return View(blogPost);
        }

        // GET: Blog/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var blogPost = await _context.BlogPosts.FindAsync(id);
            if (blogPost == null)
            {
                return NotFound();
            }

            ViewBag.TagsInput = string.Join(", ", blogPost.Tags ?? new List<string>());
            return View(blogPost);
        }

        // POST: Blog/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, BlogPost blogPost, IFormFile? FeaturedImageFile, string? TagsInput)
        {
            if (id != blogPost.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingPost = await _context.BlogPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
                    if (existingPost == null)
                    {
                        return NotFound();
                    }

                    // Xử lý upload ảnh mới
                    if (FeaturedImageFile != null && FeaturedImageFile.Length > 0)
                    {
                        var imageUrl = await _imageService.UploadImageAsync(FeaturedImageFile, "blog-posts");
                        blogPost.FeaturedImage = imageUrl;
                    }
                    else
                    {
                        // Giữ ảnh cũ nếu không upload ảnh mới
                        blogPost.FeaturedImage = existingPost.FeaturedImage;
                    }

                    // Xử lý tags
                    if (!string.IsNullOrEmpty(TagsInput))
                    {
                        blogPost.Tags = TagsInput.Split(',')
                            .Select(t => t.Trim())
                            .Where(t => !string.IsNullOrEmpty(t))
                            .ToList();
                    }
                    else
                    {
                        blogPost.Tags = new List<string>();
                    }

                    // Giữ nguyên thông tin gốc
                    blogPost.CreatedAt = existingPost.CreatedAt;
                    blogPost.ViewCount = existingPost.ViewCount;
                    blogPost.UpdatedAt = DateTime.Now;

                    _context.Update(blogPost);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Bài viết đã được cập nhật thành công!";
                    return RedirectToAction(nameof(Details), new { id = blogPost.Id });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BlogPostExists(blogPost.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Lỗi khi cập nhật bài viết: {ex.Message}";
                }
            }

            ViewBag.TagsInput = TagsInput;
            return View(blogPost);
        }

        // GET: Blog/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var blogPost = await _context.BlogPosts.FirstOrDefaultAsync(m => m.Id == id);
            if (blogPost == null)
            {
                return NotFound();
            }

            return View(blogPost);
        }

        // POST: Blog/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var blogPost = await _context.BlogPosts.FindAsync(id);
                if (blogPost != null)
                {
                    _context.BlogPosts.Remove(blogPost);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Bài viết đã được xóa thành công!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Không tìm thấy bài viết để xóa!";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi xóa bài viết: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // ===== NEW BOOKMARK ACTIONS =====

        // API: Toggle Bookmark
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ToggleBookmark([FromBody] ToggleBookmarkRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User not found" });
            }

            try
            {
                var isBookmarked = await _bookmarkService.IsBookmarkedAsync(userId, request.Id);
                bool result;
                string message;

                if (isBookmarked)
                {
                    result = await _bookmarkService.RemoveBookmarkAsync(userId, request.Id);
                    message = result ? "Đã bỏ lưu bài viết" : "Lỗi khi bỏ lưu";
                }
                else
                {
                    result = await _bookmarkService.BookmarkPostAsync(userId, request.Id);
                    message = result ? "Đã lưu bài viết" : "Lỗi khi lưu bài viết";
                }

                var bookmarkCount = await _bookmarkService.GetBookmarkCountAsync(request.Id);
                var newIsBookmarked = await _bookmarkService.IsBookmarkedAsync(userId, request.Id);

                return Json(new
                {
                    success = result,
                    message = message,
                    isBookmarked = newIsBookmarked,
                    bookmarkCount = bookmarkCount
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // API: Check if bookmarked
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> IsBookmarked(int postId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { isBookmarked = false });
            }

            try
            {
                var isBookmarked = await _bookmarkService.IsBookmarkedAsync(userId, postId);
                var bookmarkCount = await _bookmarkService.GetBookmarkCountAsync(postId);

                return Json(new
                {
                    isBookmarked = isBookmarked,
                    bookmarkCount = bookmarkCount
                });
            }
            catch (Exception ex)
            {
                return Json(new { isBookmarked = false, error = ex.Message });
            }
        }

        // ===== ADMIN PUBLISH/UNPUBLISH ACTIONS =====

        // API: Toggle Publish Status
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> TogglePublish([FromBody] TogglePublishRequest request)
        {
            try
            {
                var blogPost = await _context.BlogPosts.FindAsync(request.Id);
                if (blogPost == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy bài viết" });
                }

                blogPost.IsPublished = request.Publish;
                blogPost.UpdatedAt = DateTime.Now;

                _context.Update(blogPost);
                await _context.SaveChangesAsync();

                var message = request.Publish ? "Bài viết đã được xuất bản" : "Bài viết đã được chuyển thành bản nháp";

                return Json(new
                {
                    success = true,
                    message = message,
                    isPublished = blogPost.IsPublished
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // API: Batch Toggle Publish (for multiple posts)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BatchTogglePublish([FromBody] BatchTogglePublishRequest request)
        {
            try
            {
                var posts = await _context.BlogPosts
                    .Where(p => request.PostIds.Contains(p.Id))
                    .ToListAsync();

                if (!posts.Any())
                {
                    return Json(new { success = false, message = "Không tìm thấy bài viết nào" });
                }

                foreach (var post in posts)
                {
                    post.IsPublished = request.Publish;
                    post.UpdatedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                var message = request.Publish
                    ? $"Đã xuất bản {posts.Count} bài viết"
                    : $"Đã chuyển {posts.Count} bài viết thành bản nháp";

                return Json(new
                {
                    success = true,
                    message = message,
                    updatedCount = posts.Count
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // ===== EXISTING ACTIONS =====

        // API: Upload ảnh cho TinyMCE
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UploadImage(IFormFile image)
        {
            try
            {
                if (image != null && image.Length > 0)
                {
                    var imageUrl = await _imageService.UploadImageAsync(image, "blog-content");
                    return Json(new { success = true, url = imageUrl });
                }
                return Json(new { success = false, message = "Không có file được chọn" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private bool BlogPostExists(int id)
        {
            return _context.BlogPosts.Any(e => e.Id == id);
        }
    }

    // ===== REQUEST MODELS =====
    public class ToggleBookmarkRequest
    {
        public int Id { get; set; }
    }

    public class TogglePublishRequest
    {
        public int Id { get; set; }
        public bool Publish { get; set; }
    }

    public class BatchTogglePublishRequest
    {
        public List<int> PostIds { get; set; } = new List<int>();
        public bool Publish { get; set; }
    }
}