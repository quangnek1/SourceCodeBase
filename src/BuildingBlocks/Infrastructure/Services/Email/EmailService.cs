using Microsoft.AspNetCore.Identity.UI.Services;
using Serilog;
using Shared.Options;

namespace Infrastructure.Services.Email;
public class EmailService : IEmailSender
{
    private readonly EmailOptions _emailOptions;
    private readonly ILogger _logger;
    public EmailService(EmailOptions _emailOptions, ILogger logger)
    {
        _emailOptions = _emailOptions ?? throw new ArgumentNullException(nameof(_emailOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    }
    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        throw new NotImplementedException();
    }
}
