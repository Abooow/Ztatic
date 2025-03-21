using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ztatic;

public static class ZtaticExtensions
{
    public static IServiceCollection AddZtaticService(this IServiceCollection services, Action<ZtaticOptions>? configureOptions = null)
    {
        var options = new ZtaticOptions();
        configureOptions?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<ZtaticService>();
        
        return services;
    }

    public static void UseZtaticGenerator(this WebApplication app, bool shutdownApp = false)
    {
        var logger = app.Services.GetRequiredService<ILogger<ZtaticService>>();
        var ztaticService = app.Services.GetRequiredService<ZtaticService>();

        if (ztaticService.Options.SuppressFileGeneration)
        {
            logger.LogInformation("Ztatic is disabled.");
            return;
        }

        var assets = StaticWebAssetsFinder.FindDefaultStaticAssets(app.Environment.WebRootFileProvider);
        ztaticService.Options.ContentToCopyToOutput.AddRange(assets);
        
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Register(async void () =>
            {
                // Clear and create output folder.
                if (Directory.Exists(ztaticService.Options.OutputFolderPath))
                    Directory.Delete(ztaticService.Options.OutputFolderPath, true);
                Directory.CreateDirectory(ztaticService.Options.OutputFolderPath);
                
                // Generate pages.
                try
                {
                    await ztaticService.GenerateStaticPagesAsync(app.Urls.First()).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while generating static pages: {ErrorMessage}", ex.Message);
                }

                // Copy assets.
                try
                {
                    ztaticService.CopyAssetsToOutput();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while copying assets to output: {ErrorMessage}", ex.Message);
                }
                
                if(shutdownApp)
                    lifetime.StopApplication();
            }
        );
    }
}