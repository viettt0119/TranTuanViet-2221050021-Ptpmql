---
name: identity-email
description: Tích hợp ASP.NET Core Identity, khởi tạo tài khoản và role mặc định, cấu hình gửi email (xác thực tài khoản, quên mật khẩu) bằng SMTP, giữ bí mật cấu hình bằng User Secrets.
---

# Skill: Identity + Email

## 1. Cài đặt Identity
```csharp
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
{
    o.SignIn.RequireConfirmedAccount = true;        // phải xác thực email mới đăng nhập
    o.User.RequireUniqueEmail = true;
    o.Password.RequiredLength = 8;
    o.Lockout.MaxFailedAccessAttempts = 5;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();                         // BẮT BUỘC cho confirm email / reset password
```
Thứ tự middleware: `UseAuthentication()` rồi `UseAuthorization()`.

## 2. Tài khoản & role mặc định (seed khi khởi động)
```csharp
public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider sp, IConfiguration cfg)
    {
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        if (!await roles.RoleExistsAsync("Admin")) await roles.CreateAsync(new IdentityRole("Admin"));

        var email = cfg["SeedAdmin:Email"]!;
        if (await users.FindByEmailAsync(email) == null)
        {
            var admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var r = await users.CreateAsync(admin, cfg["SeedAdmin:Password"]!);   // lấy từ User Secrets / biến môi trường
            if (r.Succeeded) await users.AddToRoleAsync(admin, "Admin");
        }
    }
}
```
Gọi trong `Program.cs`: `using var scope = app.Services.CreateScope(); await IdentitySeeder.SeedAsync(scope.ServiceProvider, app.Configuration);`
**Không** ghi mật khẩu admin trực tiếp trong code/ appsettings.json đẩy lên Git.

## 3. Gửi email
`appsettings.json` (chỉ cấu trúc, KHÔNG có mật khẩu):
```json
"Email": { "Host": "smtp.gmail.com", "Port": 587, "From": "no-reply@example.com", "User": "", "Password": "" }
```
Giá trị thật: `dotnet user-secrets set "Email:Password" "<app-password>"` (hoặc biến môi trường khi deploy).

```csharp
public class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _o;
    public SmtpEmailSender(IOptions<EmailOptions> o) => _o = o.Value;

    public async Task SendEmailAsync(string to, string subject, string htmlMessage)
    {
        var msg = new MimeMessage();
        msg.From.Add(MailboxAddress.Parse(_o.From));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = subject;
        msg.Body = new BodyBuilder { HtmlBody = htmlMessage }.ToMessageBody();

        using var smtp = new MailKit.Net.Smtp.SmtpClient();
        await smtp.ConnectAsync(_o.Host, _o.Port, MailKit.Security.SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(_o.User, _o.Password);
        await smtp.SendAsync(msg);
        await smtp.DisconnectAsync(true);
    }
}
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
```
(Gói NuGet: `MailKit`. Dùng Gmail cần App Password; môi trường production nên dùng dịch vụ như SendGrid/Mailgun.)

## 4. Luồng cần có
- **Đăng ký → gửi email xác thực**: `GenerateEmailConfirmationTokenAsync` → mã hoá `WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))` → link `ConfirmEmail?userId=..&code=..`.
- **Quên mật khẩu**: `GeneratePasswordResetTokenAsync` → link `ResetPassword`; không tiết lộ email có tồn tại hay không (luôn báo "nếu email tồn tại, chúng tôi đã gửi liên kết").
- Gửi lại email xác thực; đổi mật khẩu; khoá tài khoản sau nhiều lần sai.
- Nội dung email tiếng Việt, có thời hạn token.

## Quy tắc
- Gửi email không chặn request quá lâu; cân nhắc hàng đợi nền (`BackgroundService`/Channel).
- Bắt lỗi SMTP, log, báo người dùng thân thiện.
- Kiểm thử bằng smtp4dev/Papercut ở môi trường dev.
