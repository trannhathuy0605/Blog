namespace Blog.Services
{
    public interface IImageService
    {
        Task<string> UploadImageAsync(IFormFile image, string folder = "blog");
        bool DeleteImage(string imagePath);
        string GetImageUrl(string imagePath);
        bool IsValidImageFile(IFormFile file);
    }
}