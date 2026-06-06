using Shared.DTOs.BackgroundJobs;

namespace Contracts.BackgroundJobs;
public interface IScheduledJobsClient
{
    Task<string?> SendReminderEmailAsync(ReminderEmailDto model);
    Task DeleteJobAsync(string jobId);
}
