using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ??
        throw new InvalidOperationException("Connection string 'DefaultConnection' not found.")));

builder.Services.AddScoped<PermissionCatalogService>();
builder.Services.AddScoped<StudentCodeGenerator>();
builder.Services.AddScoped<StudentService>();
builder.Services.AddScoped<ExcelImportService>();
builder.Services.AddMemoryCache();

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequiredUniqueChars = 4;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        // Người dùng tự đăng ký phải bấm liên kết xác thực gửi tới email trước khi đăng nhập.
        options.SignIn.RequireConfirmedEmail = true;
        options.Tokens.EmailConfirmationTokenProvider = "EmailConfirmation";
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddErrorDescriber<VietnameseIdentityErrorDescriber>()
    .AddDefaultTokenProviders()
    .AddTokenProvider<EmailConfirmationTokenProvider<ApplicationUser>>("EmailConfirmation");

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = AuthEmailTemplateRenderer.PasswordResetLinkLifetime);
builder.Services.Configure<EmailConfirmationTokenProviderOptions>(options =>
{
    options.Name = "EmailConfirmationTokenProvider";
    options.TokenLifespan = AuthEmailTemplateRenderer.ConfirmationLinkLifetime;
});

// Shared hosting (MonsterASP/IIS) không lưu được key mặc định: key mất sau mỗi lần recycle làm
// cookie đăng nhập, anti-forgery và liên kết xác thực email/đặt lại mật khẩu bị vô hiệu.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("DemoMVC");
var dataProtectionKeysPath = DataProtectionKeyPath.Resolve(builder.Configuration, builder.Environment);
if (dataProtectionKeysPath is not null)
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

// Kiểm tra lại cookie mỗi phút: gán/gỡ role, khóa tài khoản, đổi mật khẩu có hiệu lực gần như ngay lập tức.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(1));
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    // SameAsRequest: cookie có cờ Secure khi chạy HTTPS, nhưng vẫn đăng nhập được nếu hosting miễn phí chưa bật HTTPS.
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddOptions<IdentitySeedOptions>()
    .Bind(builder.Configuration.GetSection(IdentitySeedOptions.SectionName));
builder.Services.AddSingleton<IValidateOptions<IdentitySeedOptions>, IdentitySeedOptionsValidator>();
builder.Services.AddOptions<EmailSettings>()
    .Bind(builder.Configuration.GetSection(EmailSettings.SectionName));
builder.Services.AddSingleton<IValidateOptions<EmailSettings>, EmailSettingsValidator>();

builder.Services.AddScoped<IdentitySeeder>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IEmailTransport, MailKitEmailTransport>();
builder.Services.AddSingleton<AuthEmailTemplateRenderer>();
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<PermissionAuthorizationFilter>();
});
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("AuthEndpoints", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();
app.Logger.LogInformation(
    "Data Protection keys: {KeyLocation}",
    dataProtectionKeysPath ?? "default per-user key ring (configure DataProtection:KeysPath on shared hosting)");

// Thiếu PublicBaseUrl thì liên kết trong email được tạo theo header Host của request, có thể bị giả mạo.
if (!app.Environment.IsDevelopment() &&
    string.IsNullOrWhiteSpace(app.Configuration[$"{EmailSettings.SectionName}:{nameof(EmailSettings.PublicBaseUrl)}"]))
{
    app.Logger.LogWarning(
        "Email:PublicBaseUrl is not configured, so links in account emails follow the request Host header, which can be spoofed. " +
        "Set Email__PublicBaseUrl to the public address of the site.");
}

// Configure the HTTP request pipeline.
OfficeOpenXml.ExcelPackage.License.SetNonCommercialOrganization("DemoMVC");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Trang lỗi tuỳ biến cho URL sai (404), không có quyền (403)... ở mọi môi trường, kể cả khi chạy local.
// Lỗi exception khi Development vẫn hiện trang chi tiết của ASP.NET Core để dễ debug.
app.UseStatusCodePagesWithReExecute("/Home/StatusCode/{0}");

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapGet("/healthz", () => Results.Text("Healthy")).AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
    var permissionCatalog = scope.ServiceProvider.GetRequiredService<PermissionCatalogService>();
    await permissionCatalog.SyncAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var identitySeedOptions = app.Configuration
        .GetSection(IdentitySeedOptions.SectionName)
        .Get<IdentitySeedOptions>() ?? new IdentitySeedOptions();
    var canSeedAdmin =
        !string.IsNullOrWhiteSpace(identitySeedOptions.Email) &&
        !string.IsNullOrWhiteSpace(identitySeedOptions.Password) &&
        !string.IsNullOrWhiteSpace(identitySeedOptions.FullName);

    await permissionCatalog.EnsureSystemRolesAsync();

    if (canSeedAdmin)
    {
        try
        {
            await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or OptionsValidationException)
        {
            // Không làm sập website (lỗi 500.30 trên IIS) chỉ vì cấu hình admin sai; ghi log để sửa.
            app.Logger.LogCritical(
                "Could not seed the administrator account: {Reason}",
                ex is OptionsValidationException validation ? string.Join(" ", validation.Failures) : ex.Message);
        }
    }
    else
    {
        app.Logger.LogWarning(
            "Administrator account was not seeded. Configure AdminSeed:Email, AdminSeed:Password and AdminSeed:FullName " +
            "(on MonsterASP: Scripting > Environment variables, e.g. AdminSeed__Password) and restart the app.");
    }

}


app.Run();
