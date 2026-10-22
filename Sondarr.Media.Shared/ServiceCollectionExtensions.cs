using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sondarr.Media.Shared.Storage;
using Sondarr.Media.Shared.Uploads;

namespace Sondarr.Media.Shared;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// S3 storage (section "S3") and the multipart upload flow (section "Uploads").
    /// The host boots without S3 configured; upload calls then fail with
    /// <c>503 NOT_CONFIGURED</c>.
    /// </summary>
    public static IServiceCollection AddSondarrMedia(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<S3Settings>(configuration.GetSection(S3Settings.SectionName));
        services.Configure<UploadOptions>(configuration.GetSection(UploadOptions.SectionName));
        services.AddSingleton<IMediaStorage, S3MediaStorage>();
        services.AddSingleton<IMediaUploadService, MediaUploadService>();
        return services;
    }
}
