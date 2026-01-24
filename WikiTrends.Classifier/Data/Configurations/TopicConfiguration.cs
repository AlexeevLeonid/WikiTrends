using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WikiTrends.Classifier.Data.Entities;

namespace WikiTrends.Classifier.Data.Configurations;

public sealed class TopicConfiguration : IEntityTypeConfiguration<TopicEntity>
{
    public void Configure(EntityTypeBuilder<TopicEntity> builder)
    {
        //  1. Настроить таблицу и ключи (Id)
        //  2. Настроить required поля Name/Path и их длины
        //  3. Добавить уникальный индекс по Path
        builder.ToTable("topics");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Path)
            .HasColumnType("text")
            .IsRequired();

        builder.HasIndex(x => x.Path)
            .HasDatabaseName("topics_path_ix")
            .IsUnique();
    }
}
