using System.ComponentModel.DataAnnotations;

namespace WikiTrends.Infrastructure.Configuration;

public sealed class ServiceUrlsOptions
{
    public const string SectionName = "ServiceUrls";

    [Required]
    public string AggregatorBaseUrl { get; set; } = "http://localhost:5080";

    [Required]
    public string AnalyticsBaseUrl { get; set; } = "http://localhost:5081";

    [Required]
    public string ClassifierBaseUrl { get; set; } = "http://localhost:5082";

    [Required]
    public string EnricherBaseUrl { get; set; } = "http://localhost:5083";
}
