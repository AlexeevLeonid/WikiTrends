using Serilog;
using WikiTrends.Enricher;
using WikiTrends.Enricher.Data;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Logging;
using WikiTrends.Infrastructure.Persistence.Extensions;

SerilogExtensions.CreateBootstrapLogger("WikiEnricher");

try
{
    var builder = Host.CreateApplicationBuilder(args);

    // Logging
    builder.Services.AddSerilog(configuration =>
    {
        configuration
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Service", "WikiEnricher");
    });

    builder.Services.AddEnricherServices(builder.Configuration);

    var host = builder.Build();

    var databaseOptions = host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>().Value;
    if (databaseOptions.MigrateOnStartup)
    {
        await host.Services.MigrateDbAsync<EnricherDbContext>();
    }

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "WikiEnricher terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
