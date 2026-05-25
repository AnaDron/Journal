using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventJournal.Infrastructure;

public sealed class JournalChangeEntityConfiguration : IEntityTypeConfiguration<JournalChangeEntity> {
	public void Configure(EntityTypeBuilder<JournalChangeEntity> builder) {
		builder.ToTable("journal_changes");
		builder.HasKey(x => x.Id);
		builder.Property(x => x.EntityType).IsRequired().HasMaxLength(128);
		builder.Property(x => x.EntityKey).IsRequired().HasMaxLength(512);
		builder.Property(x => x.Operation).HasConversion<int>().IsRequired();
		builder.Property(x => x.FieldsJson).HasColumnType("jsonb");

		builder.HasIndex(x => x.EventId);
		builder.HasIndex(x => new { x.EntityType, x.EntityKey });
	}
}
