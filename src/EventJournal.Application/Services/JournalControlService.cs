using System.Reflection;
using System.Text.Json;
using EventJournal.Core;

namespace EventJournal.Application;

public sealed class JournalControlService(IJournalEventRepository eventRepository) : IJournalControlService {
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private JournalEvent? _pendingEvent;
	private bool _pendingEventAdded;

	public IJournalEventScope BeginEvent(JournalEventScopeOptions options) {
		var id = Guid.NewGuid();
		var utcNow = DateTimeOffset.UtcNow;
		_pendingEvent = new JournalEvent {
			Id = id,
			CreatedAtUtc = utcNow,
			OccurredAtUtc = options.OccurredAtUtc,
			Reason = options.Reason,
			InitiatorKind = options.InitiatorKind,
			InitiatorId = options.InitiatorId,
			Changes = new List<JournalChange>(),
		};
		_pendingEventAdded = false;
		var ctx = new JournalExecutionContext(id, options);
		JournalScope.Push(ctx);
		return new JournalEventScope(id);
	}

	public void AddChange(JournalChange change) {
		var scope = JournalScope.Current;
		if (scope is null) {
			return;
		}

		EnsureEventAdded();
		eventRepository.AddChange(change with { EventId = scope.EventId });
	}

	public void Add<T>(string key, T entity) =>
		AddOperation<T>(JournalOperation.Create, key, BuildCreateFields(entity));

	public void Update<T>(string key, T current, T updated) =>
		AddOperation<T>(JournalOperation.Update, key, BuildUpdateFields(current, updated));

	public void Remove<T>(string key) =>
		AddOperation<T>(JournalOperation.Delete, key, fields: null);

	private void EnsureEventAdded() {
		if (_pendingEvent is null || _pendingEventAdded) {
			return;
		}

		eventRepository.Add(_pendingEvent);
		_pendingEventAdded = true;
	}

	private void AddOperation<T>(
		JournalOperation operation,
		string key,
		IReadOnlyList<JournalFieldChange>? fields) {
		var scope = JournalScope.Current;
		if (scope is null) {
			return;
		}

		EnsureEventAdded();

		var entityType = typeof(T).Name;
		var change = new JournalChange(
			Id: Guid.NewGuid(),
			EventId: scope.EventId,
			EntityType: entityType,
			EntityKey: key,
			Operation: operation,
			Fields: fields);
		eventRepository.AddChange(change);
	}

	private static IReadOnlyList<JournalFieldChange> BuildCreateFields<T>(T entity) {
		if (entity is null) {
			return Array.Empty<JournalFieldChange>();
		}

		var type = typeof(T);
		var list = new List<JournalFieldChange>();
		foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
			if (!property.CanRead || property.GetIndexParameters().Length > 0) {
				continue;
			}

			var value = property.GetValue(entity);
			var valueJson = SerializeValue(value, property.PropertyType);
			if (valueJson is null) {
				continue;
			}

			list.Add(new JournalFieldChange(ToCamelCase(property.Name), valueJson));
		}

		return list;
	}

	private static IReadOnlyList<JournalFieldChange> BuildUpdateFields<T>(T current, T updated) {
		if (current is null || updated is null) {
			return Array.Empty<JournalFieldChange>();
		}

		var type = typeof(T);
		var list = new List<JournalFieldChange>();
		foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
			if (!property.CanRead || property.GetIndexParameters().Length > 0) {
				continue;
			}

			var currentValue = property.GetValue(current);
			var updatedValue = property.GetValue(updated);
			if (AreValuesEquivalent(currentValue, updatedValue)) {
				continue;
			}

			var valueJson = SerializeValue(updatedValue, property.PropertyType);
			list.Add(new JournalFieldChange(ToCamelCase(property.Name), valueJson));
		}

		return list;
	}

	private static bool AreValuesEquivalent(object? left, object? right) {
		if (ReferenceEquals(left, right)) {
			return true;
		}

		if (left is null || right is null) {
			return false;
		}

		if (left is string leftString && right is string rightString) {
			return string.Equals(leftString, rightString, StringComparison.Ordinal);
		}

		if (left is System.Collections.IEnumerable leftEnumerable &&
		    right is System.Collections.IEnumerable rightEnumerable) {
			return EnumerablesSequenceEqual(leftEnumerable, rightEnumerable);
		}

		return Equals(left, right);
	}

	private static bool EnumerablesSequenceEqual(
		System.Collections.IEnumerable left,
		System.Collections.IEnumerable right) {
		var leftEnumerator = left.GetEnumerator();
		var rightEnumerator = right.GetEnumerator();
		while (true) {
			var leftMoved = leftEnumerator.MoveNext();
			var rightMoved = rightEnumerator.MoveNext();
			if (leftMoved != rightMoved) {
				return false;
			}

			if (!leftMoved) {
				return true;
			}

			if (!Equals(leftEnumerator.Current, rightEnumerator.Current)) {
				return false;
			}
		}
	}

	private static string? SerializeValue(object? value, Type valueType) {
		if (value is null) {
			return null;
		}

		var element = JsonSerializer.SerializeToElement(value, valueType, JsonOptions);
		return element.ValueKind == JsonValueKind.Null ? null : element.GetRawText();
	}

	private static string ToCamelCase(string value) {
		if (string.IsNullOrEmpty(value) || char.IsLower(value[0])) {
			return value;
		}

		return char.ToLowerInvariant(value[0]) + value[1..];
	}

	private sealed class JournalEventScope(Guid eventId) : IJournalEventScope {
		private bool _disposed;
		public Guid EventId => eventId;

		public void Dispose() {
			if (_disposed) {
				return;
			}

			JournalScope.Pop();
			_disposed = true;
		}
	}
}
