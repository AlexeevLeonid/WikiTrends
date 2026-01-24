namespace WikiTrends.Analytics.ClickHouse;

public sealed class ClickHouseSettings
{
    public const string SectionName = "ClickHouse";

    [System.ComponentModel.DataAnnotations.Required]
    public string ConnectionString { get; set; } = "Host=localhost;Port=8123;Database=wikitrends;Protocol=http";

    [System.ComponentModel.DataAnnotations.Required]
    public string BaseUrl { get; set; } = "http://localhost:8123";

    [System.ComponentModel.DataAnnotations.Required]
    public string Database { get; set; } = "wikitrends";
    public string? User { get; set; }
    public string? Password { get; set; }
}
