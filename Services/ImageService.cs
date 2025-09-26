using Blog.Services;

namespace Blog.Services
{
    public class ImageService : IImageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ImageService> _logger;
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private const long MaxFileSize = 5 * 1024 * 1024; // 5MB

        public ImageService(IWebHostEnvironment environment, ILogger<ImageService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<string> UploadImageAsync(IFormFile image, string folder = "blog")
        {
            if (!IsValidImageFile(image))
            {
                throw new ArgumentException("File không hợp lệ");
            }

            // Tạo tên file unique
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", folder);

            // Tạo thư mục nếu chưa tồn tại
            Directory.CreateDirectory(uploadsFolder);

            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(fileStream);
            }

            return $"/images/{folder}/{fileName}";
        }

        public bool DeleteImage(string imagePath)
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath)) return false;

                var fullPath = Path.Combine(_environment.WebRootPath, imagePath.TrimStart('/'));
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa ảnh: {ImagePath}", imagePath);
            }
            return false;
        }

        public string GetImageUrl(string imagePath)
        {
            return string.IsNullOrEmpty(imagePath) ? "/images/placeholder.jpg" : imagePath;
        }

        public bool IsValidImageFile(IFormFile file)
        {
            if (file == null || file.Length == 0) return false;
            if (file.Length > MaxFileSize) return false;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return _allowedExtensions.Contains(extension);
        }
    }
}