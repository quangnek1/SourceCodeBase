using System.ComponentModel.DataAnnotations;

namespace Shared.Options;

public class RedisOptions
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    // Prefix added to every key, so several apps can share one Redis instance.
    public string InstanceName { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int DefaultExpirationMinutes { get; set; } = 10;
}
