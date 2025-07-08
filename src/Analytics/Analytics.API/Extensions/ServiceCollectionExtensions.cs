using Analytics.API.Services;
using Analytics.Storage;

namespace Analytics.API.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAnalyticsServices(this IServiceCollection services)
        {
            services.AddTransient<IAnalyticsService, AnalyticsService>();
            services.AddDbContext<AnalyticsDbContext>();
            services.AddSingleton<IGeoLocationService, GeoLocationService>();
            
            return services;
        }
    }
}