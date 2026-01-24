using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using WikiTrends.Classifier;
using WikiTrends.Classifier.Configuration;
using WikiTrends.Classifier.Data;
using WikiTrends.Classifier.Seed;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Logging;
using WikiTrends.Infrastructure.Persistence.Extensions;

SerilogExtensions.CreateBootstrapLogger("WikiClassifier");

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog(configuration =>
    {
        configuration
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Service", "WikiClassifier");
    });

    builder.Services.AddClassifierServices(builder.Configuration);

    var host = builder.Build();

    await host.Services.MigrateDbAsync<ClassifierDbContext>();

    try
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClassifierDbContext>();
        await db.Database.ExecuteSqlRawAsync(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'topics'
          AND column_name = 'path'
    ) THEN
        EXECUTE 'ALTER TABLE topics ALTER COLUMN path TYPE text';
    ELSIF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'topics'
          AND column_name = 'Path'
    ) THEN
        EXECUTE 'ALTER TABLE topics ALTER COLUMN ""Path"" TYPE text';
    END IF;
END $$;
");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Failed to ensure topics.path is text");
    }

    using (var scope = host.Services.CreateScope())
    {
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ClassifierOptions>>().Value;
        if (options.SeedTopicsOnStartup)
        {
            var seeder = scope.ServiceProvider.GetRequiredService<TopicSeeder>();
            await seeder.SeedAsync();
        }
    }

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "WikiClassifier terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
