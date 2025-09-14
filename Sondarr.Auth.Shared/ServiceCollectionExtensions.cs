using Microsoft.Extensions.DependencyInjection;
using Sondarr.Auth.Shared.Services;

namespace Sondarr.Auth.Shared
{
    /// <summary>
    /// Extension methods for configuring Sondarr.Auth.Shared services.
    /// This class provides convenient methods for registering authentication-related services
    /// in the dependency injection container.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the UserContextService to the service collection.
        /// This service provides access to current user information from JWT claims.
        /// </summary>
        /// <param name="services">The IServiceCollection to add services to.</param>
        /// <returns>The IServiceCollection so that additional calls can be chained.</returns>
        public static IServiceCollection AddUserContextService(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddScoped<IUserContextService, UserContextService>();
            return services;
        }

        /// <summary>
        /// Adds all Sondarr.Auth.Shared services to the service collection.
        /// This includes the UserContextService and HTTP context accessor.
        /// </summary>
        /// <param name="services">The IServiceCollection to add services to.</param>
        /// <returns>The IServiceCollection so that additional calls can be chained.</returns>
        public static IServiceCollection AddSondarrAuthServices(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddScoped<IUserContextService, UserContextService>();
            return services;
        }
    }
}
