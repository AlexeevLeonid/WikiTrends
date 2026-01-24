using Serilog;
using WikiTrends.Gateway;
using WikiTrends.Gateway.Hubs;
using WikiTrends.Gateway.Configuration;
using WikiTrends.Gateway.Middleware;
using WikiTrends.Infrastructure.Logging;

SerilogExtensions.CreateBootstrapLogger("WikiGateway");

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog(configuration =>
    {
        configuration
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Service", "WikiGateway");
    });

    builder.Services.AddGatewayServices(builder.Configuration);

    var app = builder.Build();

    app.UseMiddleware<ExceptionHandlingMiddleware>();

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
    app.MapHub<TrendHub>("/hubs/trends");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "WikiGateway terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
