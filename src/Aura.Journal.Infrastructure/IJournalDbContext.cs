using Microsoft.EntityFrameworkCore;

namespace Aura.Journal.Infrastructure;

/// <summary>
/// Контракт хранилища журнала: консьюмерский <see cref="DbContext"/> предоставляет журналу
/// его две таблицы. SDK работает поверх этого интерфейса; конкретный <c>DbContext</c> может
/// содержать и другие сущности консьюмера — это не имеет значения для журнала.
/// </summary>
public interface IJournalDbContext {
	DbSet<JournalEventEntity> JournalEvents { get; }

	DbSet<JournalChangeEntity> JournalChanges { get; }
}
