using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WikiTrends.Enricher.Data.Entities;

namespace WikiTrends.Enricher.Data.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<CategoryEntity>
{
    public void Configure(EntityTypeBuilder<CategoryEntity> builder)
    {
        //  1. Настроить таблицу (например, "categories") и первичный ключ
        //  2. Настроить required поле Name, длину и нормализацию (trim/lower) при необходимости
        //  3. Настроить FK на ArticleEntity (ArticleId) и каскадное поведение
        //  4. Добавить индекс по ArticleId для быстрого получения категорий статьи
        //  5. Добавить уникальный индекс по (ArticleId, Name) чтобы не было дублей
        //  1. Настроить таблицу и первичный ключ
        builder.HasKey(x => x.Id);

        // 2. Нормализация и ограничения
        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(255);

        // 3. Связь и удаление
        // Удаляем категорию, если удалена статья.
        // В Postgres это создаст CONSTRAINT ... ON DELETE CASCADE
        builder.HasOne(x => x.Article)
            .WithMany(x => x.Categories)
            .HasForeignKey(x => x.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        // 4. Индекс FK (EF Core часто создает его сам, но явно надежнее)
        builder.HasIndex(x => x.ArticleId);

        // 5. Уникальность категории внутри статьи
        // Чтобы сервис обогащения не добавил категорию "Космос" дважды к одной статье.
        builder.HasIndex(x => new { x.ArticleId, x.Name })
            .IsUnique();
    }
}
