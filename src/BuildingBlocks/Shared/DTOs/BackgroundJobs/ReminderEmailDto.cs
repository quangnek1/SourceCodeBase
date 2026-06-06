namespace Shared.DTOs.BackgroundJobs;
public record ReminderEmailDto(string Email, string Subject, string Content, DateTimeOffset EnqueueAt);
