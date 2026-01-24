using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WikiTrends.Classifier.Data.Entities;

namespace WikiTrends.Classifier.Data.Configurations;

public sealed class WikidataMappingConfiguration : IEntityTypeConfiguration<WikidataMappingEntity>
{
    public void Configure(EntityTypeBuilder<WikidataMappingEntity> builder)
    {
        //  1. Настроить таблицу и ключи
        //  2. Настроить required поля Wiki/Title и опциональный WikidataId
        //  3. Добавить индекс/уникальность по (Wiki, Title)
        //  4. Настроить CachedAt
        builder.Property(x => x.Wiki).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new {x.Wiki, x.Title}).HasDatabaseName("wikidata_mapping_wiki_title_ix").IsUnique();
        
    }
}
