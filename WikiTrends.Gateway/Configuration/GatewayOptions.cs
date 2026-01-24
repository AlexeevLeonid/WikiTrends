namespace WikiTrends.Gateway.Configuration;

public sealed class GatewayOptions
{
    public const string SectionName = "Gateway";

    public bool EnableSwagger { get; set; } = true;
}
