using Aura.Journal.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aura.Journal.Infrastructure;

/// <summary>
/// DI-расширения Infrastructure-слоя SDK журнала. Регистрирует репозиторий журнала поверх
/// консьюмерского <see cref="DbContext"/>, который должен реализовать <see cref="IJournalDbContext"/>.
/// </summary>
public static class DependencyInjection {
	/// <summary>
	/// Регистрирует <see cref="IJournalEventRepository"/> и мост <see cref="IJournalDbContext"/>
	/// на консьюмерский <typeparamref name="TDbContext"/>.
	/// </summary>
	public static IServiceCollection AddJournalInfrastructure<TDbContext>(this IServiceCollection services)
		where TDbContext : DbContext, IJournalDbContext {
		services.AddScoped<IJournalDbContext>(sp => sp.GetRequiredService<TDbContext>());
		services.AddScoped<IJournalEventRepository, JournalEventRepository>();
		return services;
	}
}
