namespace WikiTrends.Frontend.Configuration;

public sealed class GatewayOptions
{
    public const string SectionName = "Gateway";

    public string BaseUrl { get; init; } = "http://localhost:5080";
}
