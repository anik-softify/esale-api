namespace eSale.Application.Common.BackgroundJobs;

public interface IEmailJobService
{
    Task QueueWelcomeEmailAsync(string toEmail, string customerName, CancellationToken cancellationToken = default);
    Task SendWelcomeEmailAsync(string toEmail, string customerName, CancellationToken cancellationToken = default);
}
