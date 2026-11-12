using DemoMVC.Models;

namespace DemoMVC.Services;

public interface IEmailTransport
{
    Task SendAsync(
        EmailSettings settings,
        string email,
        string subject,
        string htmlMessage,
        CancellationToken cancellationToken);
}
