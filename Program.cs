using Blog.Data;
using Blog.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Cấu hình Identity với Roles
builder.Services.AddDefaultIdentity<IdentityUser>(options => {
    options.SignIn.RequireConfirmedAccount = false; // Tắt xác nhận email để test dễ hơn
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddRoles<IdentityRole>() // Thêm support cho Roles
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();

// Đăng ký services
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IBookmarkService, BookmarkService>();

var app = builder.Build();

// ⭐ QUAN TRỌNG: Khởi tạo roles và admin user TRƯỚC khi app.Run()
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await SeedRolesAndAdminAsync(services);
        Console.WriteLine("✅ Seed roles và admin thành công!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Lỗi khi seed data: {ex.Message}");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); // ⭐ Thêm dòng này
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();

// Seed roles và admin user
async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
{
    var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();

    Console.WriteLine("🔄 Bắt đầu seed roles và admin...");

    // Tạo roles
    string[] roleNames = { "Admin", "Guest" };
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
            Console.WriteLine($"✅ Đã tạo role: {roleName}");
        }
        else
        {
            Console.WriteLine($"ℹ️ Role {roleName} đã tồn tại");
        }
    }

    // Tạo admin user
    var adminEmail = "admin@blog.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    if (adminUser == null)
    {
        adminUser = new IdentityUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(adminUser, "Admin123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
            Console.WriteLine($"✅ Đã tạo admin user: {adminEmail}");
            Console.WriteLine($"🔑 Password: Admin123!");
        }
        else
        {
            Console.WriteLine($"❌ Lỗi tạo admin user:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"   - {error.Description}");
            }
        }
    }
    else
    {
        Console.WriteLine($"ℹ️ Admin user {adminEmail} đã tồn tại");

        // Đảm bảo admin có role Admin
        if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
            Console.WriteLine($"✅ Đã gán role Admin cho user {adminEmail}");
        }
    }
}