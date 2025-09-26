using System.ComponentModel.DataAnnotations;

namespace Blog.Models
{
    public class BlogPost
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tiêu đề là bắt buộc")]
        [StringLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự")]
        [Display(Name = "Tiêu đề")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nội dung là bắt buộc")]
        [Display(Name = "Nội dung")]
        public string Content { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Tóm tắt không được vượt quá 500 ký tự")]
        [Display(Name = "Tóm tắt")]
        public string Summary { get; set; } = string.Empty;

        [Display(Name = "Ảnh đại diện")]
        public string? FeaturedImage { get; set; }

        [Display(Name = "Mô tả ảnh")]
        [StringLength(200)]
        public string? ImageAlt { get; set; }

        [Display(Name = "Ngày tạo")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Ngày cập nhật")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [StringLength(100, ErrorMessage = "Tên tác giả không được vượt quá 100 ký tự")]
        [Display(Name = "Tác giả")]
        public string Author { get; set; } = string.Empty;

        [Display(Name = "Đã xuất bản")]
        public bool IsPublished { get; set; } = true;

        [StringLength(50, ErrorMessage = "Danh mục không được vượt quá 50 ký tự")]
        [Display(Name = "Danh mục")]
        public string Category { get; set; } = string.Empty;

        [Display(Name = "Thẻ tag")]
        public List<string> Tags { get; set; } = new List<string>();

        [Display(Name = "Lượt xem")]
        public int ViewCount { get; set; } = 0;

        [Display(Name = "Meta Description (SEO)")]
        [StringLength(160)]
        public string? MetaDescription { get; set; }
    }
}