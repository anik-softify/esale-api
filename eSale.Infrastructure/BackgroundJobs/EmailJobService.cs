using eSale.Application.Common.BackgroundJobs;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace eSale.Infrastructure.BackgroundJobs;

public sealed class EmailJobService : IEmailJobService
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<EmailJobService> _logger;

    public EmailJobService(IBackgroundJobClient backgroundJobClient, ILogger<EmailJobService> logger)
    {
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public Task QueueWelcomeEmailAsync(string toEmail, string customerName, CancellationToken cancellationToken = default)
    {
        _backgroundJobClient.Enqueue<IEmailJobService>(
            service => service.SendWelcomeEmailAsync(toEmail, customerName, CancellationToken.None));

        _logger.LogInformation("Queued welcome email for {Email}", toEmail);
        return Task.CompletedTask;
    }

    public Task SendWelcomeEmailAsync(string toEmail, string customerName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending welcome email to {Email} for {CustomerName}", toEmail, customerName);
        return Task.CompletedTask;
    }
}
