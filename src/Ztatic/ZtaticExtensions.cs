using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ztatic;

public static class ZtaticExtensions
{
    public static IServiceCollection AddZtatic(this IServiceCollection services, Action<ZtaticOptions>? configureOptions = null)
    {
        var options = new ZtaticOptions();
        configureOptions?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<ZtaticService>();
        services.AddSingleton<DiscoveredRoutes>();
        
        return services;
    }

    public static void GenerateSitemap(this ZtaticOptions options, string outputPath = "sitemap.xml")
    {
        options.AfterContentGeneratedAction += async (services, opt) =>
        {
            var logger = services.GetRequiredService<ILogger<ZtaticService>>();
            if (string.IsNullOrWhiteSpace(opt.SiteUrl))
            {
                logger.LogWarning($"{nameof(ZtaticOptions)}.{nameof(ZtaticOptions.SiteUrl)} is null or empty, can not generate sitemap.");
                return;
            }

            var discoveredRoutes = services.GetRequiredService<DiscoveredRoutes>();
            await SitemapGenerator.GenerateSitemapAsync(opt.SiteUrl, discoveredRoutes.GetDiscoveredRoutes(), Path.Combine(opt.OutputFolderPath, outputPath));
            logger.LogInformation("Generated sitemap to {OutputPath}.", Path.Combine(opt.OutputFolderPath, outputPath));
        };
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
                    await ztaticService.CopyAssetsToOutputAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while copying assets to output: {ErrorMessage}", ex.Message);
                }

                if (ztaticService.Options.AfterContentGeneratedAction is not null)
                    await ztaticService.Options.AfterContentGeneratedAction.Invoke(app.Services, ztaticService.Options);
                
                if (shutdownApp)
                    lifetime.StopApplication();
            }
        );
    }
}