using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace WikiTrends.Infrastructure.Persistence.Extensions;

/// <summary>
/// Extension methods для регистрации DbContext
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует DbContext с PostgreSQL провайдером
    /// </summary>
    /// <typeparam name="TContext">Тип DbContext</typeparam>
    /// <param name="connectionStringName">Имя connection string в конфигурации</param>
    public static IServiceCollection AddPostgresDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "PostgreSQL")
        where TContext : BaseDbContext
    {
        var connectionString = configuration.GetConnectionString(connectionStringName);
        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException("null or empty connection string");
        services.AddDbContext<TContext>(options =>
               {
                   options.UseNpgsql(connectionString, npgsqlOptions =>
                   {
                       npgsqlOptions.EnableRetryOnFailure(3);
                       npgsqlOptions.CommandTimeout(30);
                   });
                   // В Development можно включить sensitive data logging
               });
        return services;
    }

    /// <summary>
    /// Применяет pending миграции при старте приложения
    /// </summary>
    public static async Task MigrateDbAsync<TContext>(this IServiceProvider serviceProvider)
        where TContext : DbContext
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var migrations = context.Database.GetMigrations();
        if (!migrations.Any())
        {
            await context.Database.EnsureCreatedAsync();
        }
        else
        {
            await context.Database.MigrateAsync();
        }
        return;
    }
}