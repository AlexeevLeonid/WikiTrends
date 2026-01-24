using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace WikiTrends.Infrastructure.Logging;

/// <summary>
/// Extension methods для настройки Serilog
/// </summary>
public static class SerilogExtensions
{
    /// <summary>
    /// Настраивает Serilog для Host Builder
    /// </summary>
    public static IHostBuilder UseWikiTrendsSerilog(
        this IHostBuilder hostBuilder,
        string serviceName)
    {
        hostBuilder.UseSerilog((context, services, configuration) =>
         ConfigureSerilog(configuration, context.Configuration, serviceName));
        // TODO: Вернуть hostBuilder
        return hostBuilder;
    }

    /// <summary>
    /// Создаёт bootstrap logger для логирования при старте приложения
    /// </summary>
    public static void CreateBootstrapLogger(string serviceName)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console()
            .CreateBootstrapLogger();
    }

    private static void ConfigureSerilog(
        LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        string serviceName)
    {
        loggerConfiguration.ReadFrom.Configuration(configuration)
            .Enrich.WithProperty("Service", serviceName)
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.FromLogContext()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Service} | {Message:lj}{NewLine}{Exception}");

        var seqUrl = configuration["Serilog:SeqUrl"];
        if (!string.IsNullOrEmpty(seqUrl))
            loggerConfiguration.WriteTo.Seq(seqUrl);

    }
}