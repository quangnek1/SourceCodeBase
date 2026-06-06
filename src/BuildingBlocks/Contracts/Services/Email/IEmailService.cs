using Shared.DTOs.Email;

namespace Contracts.Services.Email;
public interface IEmailService
{
    Task SendAsync(
        MailRequest request,
        CancellationToken cancellationToken = default);
}
