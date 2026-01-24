using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WikiTrends.Classifier.Data.Entities;

namespace WikiTrends.Classifier.Data.Configurations;

public sealed class ArticleTopicConfiguration : IEntityTypeConfiguration<ArticleTopicEntity>
{
    public void Configure(EntityTypeBuilder<ArticleTopicEntity> builder)
    {
        //  1. Настроить таблицу и ключи
        //  2. Настроить поля ArticleId/TopicId/Confidence
        //  3. Настроить FK к TopicEntity
        //  4. Добавить индекс (ArticleId, TopicId) для быстрого поиска
        builder.ToTable("article_topics");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ArticleId)
            .IsRequired();

        builder.Property(x => x.TopicId)
            .IsRequired();

        builder.Property(x => x.Confidence)
            .IsRequired();

        builder.HasOne(x => x.Topic)
            .WithMany()
            .HasForeignKey(x => x.TopicId);

        builder.HasIndex(x => new { x.ArticleId, x.TopicId })
            .HasDatabaseName("article_topics_article_topic_ix");
    }
}
