using Serilog;
using WikiTrends.Scheduler;
using WikiTrends.Infrastructure.Logging;

SerilogExtensions.CreateBootstrapLogger("WikiScheduler");

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog(configuration =>
    {
        configuration
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Service", "WikiScheduler");
    });

    builder.Services.AddSchedulerServices(builder.Configuration);

    var app = builder.Build();

    app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    if (!string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase))
    {
        app.UseHttpsRedirection();
    }
    app.UseAuthorization();
    app.MapControllers();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "WikiScheduler terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
