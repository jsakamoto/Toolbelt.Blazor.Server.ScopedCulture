using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Toolbelt.Blazor.Server.ScopedCulture;

namespace Toolbelt.Blazor.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding I18n Text service.
/// </summary>
public static class ScopedCultureDependencyInjection
{
    /// <summary>
    ///  Adds a IScopedCulture service to the specified Microsoft.Extensions.DependencyInjection.IServiceCollection.
    /// </summary>
    /// <param name="services">The Microsoft.Extensions.DependencyInjection.IServiceCollection to add the service to.</param>
    public static IServiceCollection AddScopedCulture(this IServiceCollection services)
    {
        services.TryAddScoped<IScopedCulture, ScopedCulture>();
        return services;
    }
}
