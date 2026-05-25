using Microsoft.EntityFrameworkCore;

namespace Aura.Journal.Infrastructure;

/// <summary>
/// Расширение <see cref="ModelBuilder"/> для регистрации EF-конфигураций журнала. Консьюмер
/// вызывает <c>modelBuilder.ApplyJournalSchema()</c> в <c>OnModelCreating</c> своего <c>DbContext</c>.
/// </summary>
public static class JournalModelBuilderExtensions {
	public static ModelBuilder ApplyJournalSchema(this ModelBuilder modelBuilder) {
		modelBuilder.ApplyConfiguration(new JournalEventEntityConfiguration());
		modelBuilder.ApplyConfiguration(new JournalChangeEntityConfiguration());
		return modelBuilder;
	}
}
