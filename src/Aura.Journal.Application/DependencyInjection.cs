using Microsoft.Extensions.DependencyInjection;

namespace Aura.Journal.Application;

/// <summary>
/// DI-расширения SDK журнала. Регистрирует сервисы записи и чтения и обогащение.
/// Резолверы (имя, ссылки, форматтеры, конвенция ключей) — ответственность консьюмера:
/// каждый адаптер регистрируется как реализация соответствующего интерфейса.
/// </summary>
public static class DependencyInjection {
	public static IServiceCollection AddJournalApplication(this IServiceCollection services) {
		services.AddScoped<JournalEnricher>();
		services.AddScoped<IJournalService, JournalService>();
		services.AddScoped<IJournalControlService, JournalControlService>();
		return services;
	}
}
