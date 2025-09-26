using System.Diagnostics;
using Blog.Models;
using Blog.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Blog.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index(string? category = null, string? tag = null, string? search = null)
        {
            var query = _context.BlogPosts.Where(p => p.IsPublished);

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

            // Lấy danh sách bài viết
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

            // Chuẩn bị dữ liệu cho View
            ViewBag.BlogPosts = allPosts;
            ViewBag.SearchTerm = search;

            // Load categories
            ViewBag.Categories = await _context.BlogPosts
                .Where(p => p.IsPublished && !string.IsNullOrEmpty(p.Category))
                .Select(p => p.Category)
                .Distinct()
                .ToListAsync();

            // Load recent posts
            ViewBag.RecentPosts = await _context.BlogPosts
                .Where(p => p.IsPublished)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync();

            // Load popular tags
            var allTaggedPosts = await _context.BlogPosts
                .Where(p => p.IsPublished)
                .ToListAsync();

            var allTags = allTaggedPosts
                .SelectMany(p => p.Tags ?? new List<string>())
                .Where(t => !string.IsNullOrEmpty(t))
                .GroupBy(t => t)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => g.Key)
                .ToList();

            ViewBag.PopularTags = allTags;

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}