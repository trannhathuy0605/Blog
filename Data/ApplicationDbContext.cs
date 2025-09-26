using Blog.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Blog.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<BlogPost> BlogPosts { get; set; }
        public DbSet<Bookmark> Bookmarks { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Cấu hình entity BlogPost
            builder.Entity<BlogPost>(entity =>
            {
                // Chuyển đổi List<string> Tags thành chuỗi để lưu trong database
                entity.Property(e => e.Tags)
                    .HasConversion(
                        v => string.Join(',', v),
                        v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                    );
            });

            // Cấu hình entity Bookmark
            builder.Entity<Bookmark>(entity =>
            {
                // Tạo composite index để tránh duplicate bookmarks
                entity.HasIndex(e => new { e.UserId, e.BlogPostId })
                      .IsUnique();

                // Cấu hình relationships
                entity.HasOne(b => b.User)
                      .WithMany()
                      .HasForeignKey(b => b.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(b => b.BlogPost)
                      .WithMany()
                      .HasForeignKey(b => b.BlogPostId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
