namespace Aura.Journal.Core;

public sealed record Page<T>(IReadOnlyList<T> Items, int PageNumber, int TotalCount);
