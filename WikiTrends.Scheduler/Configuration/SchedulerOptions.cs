using System.ComponentModel.DataAnnotations;

namespace WikiTrends.Scheduler.Configuration;

public sealed class SchedulerOptions
{
    public const string SectionName = "Scheduler";

    [Range(1, 1440)]
    public int BaselineRecalculationIntervalMinutes { get; set; } = 60;

    [Range(1, 1440)]
    public int SystemHealthCheckIntervalMinutes { get; set; } = 5;
}
