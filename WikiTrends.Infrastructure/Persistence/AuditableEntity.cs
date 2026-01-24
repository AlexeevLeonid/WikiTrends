namespace WikiTrends.Infrastructure.Persistence;

/// <summary>
/// Базовый класс для всех сущностей с аудитом.
/// Автоматически заполняется в BaseDbContext.
/// </summary>
public abstract class AuditableEntity
{
    /// <summary>
    /// Первичный ключ
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Дата создания записи (UTC)
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Дата последнего обновления (UTC), null если не обновлялась
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}