using EventJournal.Core;

namespace EventJournal.Infrastructure;

public sealed class JournalChangeEntity {
	public Guid Id { get; set; }

	public Guid EventId { get; set; }

	public JournalEventEntity? Event { get; set; }

	public string EntityType { get; set; } = "";

	public string EntityKey { get; set; } = "";

	public JournalOperation Operation { get; set; }

	/// <summary>
	/// jsonb: [{ "path", "value" }]. Для операции Delete — <c>null</c>.
	/// </summary>
	public string? FieldsJson { get; set; }
}
