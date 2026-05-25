namespace Aura.Journal.Application;

public static class JournalScope {
	private static readonly AsyncLocal<JournalExecutionContext?> Context = new();

	public static JournalExecutionContext? Current => Context.Value;

	public static void Push(JournalExecutionContext executionContext) => Context.Value = executionContext;

	public static void Pop() => Context.Value = null;
}
