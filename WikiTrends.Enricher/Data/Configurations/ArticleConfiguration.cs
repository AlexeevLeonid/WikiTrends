using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WikiTrends.Enricher.Data.Entities;

namespace WikiTrends.Enricher.Data.Configurations;

public sealed class ArticleConfiguration : IEntityTypeConfiguration<ArticleEntity>
{
    public void Configure(EntityTypeBuilder<ArticleEntity> builder)
    {
        //  1. Настроить таблицу (например, "articles") и первичный ключ
        //  2. Настроить required поля Wiki/Title, максимальные длины, collation при необходимости
        //  3. Настроить поле WikiPageId (индекс для быстрых lookup)
        //  4. Добавить уникальный индекс по (Wiki, WikiPageId)
        //  5. Добавить индекс по (Wiki, Title) если используется поиск по заголовку
        //  6. Настроить связи с CategoryEntity (one-to-many)
        //  7. Настроить audit поля (CreatedAt/UpdatedAt) при необходимости
        builder.HasKey(x => x.Id);

        // 2. Поля
        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(500); // Оптимально для заголовков

        builder.Property(x => x.Wiki)
            .IsRequired()
            .HasMaxLength(10); // "ru", "en", "de" — короткие коды

        // Postgres эффективно сжимает текст, но лимит полезен
        builder.Property(x => x.Extract).HasMaxLength(4000);

        // 3. Индекс для быстрого поиска по ID страницы в Вики (основной сценарий lookup)
        builder.HasIndex(x => x.WikiPageId);

        // 4. Уникальность статьи в рамках одной вики
        // Это критично, чтобы сервис обогащения не создал дубль при повторной обработке
        builder.HasIndex(x => new { x.Wiki, x.WikiPageId })
            .IsUnique();

        // 5. Индекс для поиска по названию (композитный, так как ищем обычно внутри конкретной вики)
        // В Postgres индексы чувствительны к регистру. 
        // Если поиск будет точным:
        builder.HasIndex(x => new { x.Wiki, x.Title });

        // 6. Связь one-to-many
        builder.HasMany(x => x.Categories)
            .WithOne(x => x.Article)
            .HasForeignKey(x => x.ArticleId)
            .OnDelete(DeleteBehavior.Cascade); // <-- Важный момент

        // 7. Аудит (синтаксис Postgres)
        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()"); // Postgres функция для текущего времени

        // UpdatedAt обычно обновляется кодом приложения или триггером, 
        // но дефолтное значение не нужно.
    }
}
