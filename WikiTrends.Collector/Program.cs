using Serilog;
using WikiTrends.Collector;
using WikiTrends.Infrastructure.Logging;

internal class Program
{
    private static async Task Main(string[] args)
    {
        SerilogExtensions.CreateBootstrapLogger("WikiCollector");

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
                    .Enrich.WithProperty("Service", "WikiCollector");
            });

            builder.Services.AddCollectorServices(builder.Configuration);

            var host = builder.Build();

            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "WikiCollector terminated unexpectedly");
            throw;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}