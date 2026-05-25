using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aura.Journal.Infrastructure;

public sealed class JournalEventEntityConfiguration : IEntityTypeConfiguration<JournalEventEntity> {
	public void Configure(EntityTypeBuilder<JournalEventEntity> builder) {
		builder.ToTable("journal_events");
		builder.HasKey(x => x.Id);
		builder.Property(x => x.CreatedAtUtc).IsRequired();
		builder.Property(x => x.OccurredAtUtc);
		builder.Property(x => x.Reason).HasMaxLength(2000);
		builder.Property(x => x.InitiatorKind).HasConversion<int>().IsRequired();
		builder.Property(x => x.InitiatorId).HasMaxLength(256);

		builder.HasMany(x => x.Changes)
			.WithOne(x => x.Event!)
			.HasForeignKey(x => x.EventId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => x.CreatedAtUtc).IsDescending();
		builder.HasIndex(x => x.OccurredAtUtc).IsDescending();
	}
}
