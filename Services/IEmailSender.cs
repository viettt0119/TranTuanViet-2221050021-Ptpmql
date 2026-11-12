namespace DemoMVC.Services;

public interface IEmailSender
{
    Task SendAsync(string email, string subject, string htmlMessage, CancellationToken cancellationToken = default);
}
