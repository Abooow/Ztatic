using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ztatic.Blogs;

namespace Ztatic;

public static class ZtaticExtensions
{
    public static ZtaticBuilder AddZtatic(this IServiceCollection services, Action<ZtaticOptions>? configureOptions = null)
    {
        var options = new ZtaticOptions();
        configureOptions?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<ZtaticService>();
        services.AddSingleton<DiscoveredRoutes>();
        
        return new ZtaticBuilder(services, options);
    }

    public static ZtaticBuilder AddBlogManager(this ZtaticBuilder ztaticBuilder, Action<BlogConfigOptions>? configureOptions = null)
    {
        return AddBlogManager<BlogManager>(ztaticBuilder, configureOptions);
    }
    
    public static ZtaticBuilder AddBlogManager<TBlogManager>(this ZtaticBuilder ztaticBuilder, Action<BlogConfigOptions>? configureOptions = null)
        where TBlogManager : class, IBlogManager<BlogInfo, BlogAuthor, BlogPost<BlogInfo, BlogAuthor>, BlogSettings<BlogAuthor>>
    {
        var blogOptions = new BlogConfigOptions();
        configureOptions?.Invoke(blogOptions);
        ztaticBuilder.Services.AddSingleton(blogOptions);
        ztaticBuilder.Services.AddSingleton<TBlogManager>();

        ztaticBuilder.Options.BeforeContentGeneratedAction += async (services, opt) =>
        {
            var blogManager = services.GetRequiredService<TBlogManager>();

            await blogManager.LoadBlogSettings();
            await blogManager.ParseAndAddPostsAsync();
        };
        
        return ztaticBuilder;
    }
    
    public static ZtaticBuilder AddBlogManager<TBlogInfo, TBlogAuthor, TBlogPost, TSettings>(this ZtaticBuilder ztaticBuilder, Action<BlogConfigOptions>? configureOptions = null)
        where TBlogInfo : BlogInfo, new()
        where TBlogAuthor : BlogAuthor, new()
        where TBlogPost : BlogPost<TBlogInfo, TBlogAuthor>, new()
        where TSettings : BlogSettings<TBlogAuthor>, new()
    {
        var blogOptions = new BlogConfigOptions();
        configureOptions?.Invoke(blogOptions);
        ztaticBuilder.Services.AddSingleton(blogOptions);
        ztaticBuilder.Services.AddSingleton<BlogManager<TBlogInfo, TBlogAuthor, TBlogPost, TSettings>>();
    
        ztaticBuilder.Options.BeforeContentGeneratedAction += async (services, opt) =>
        {
            var blogManager = services.GetRequiredService<BlogManager<TBlogInfo, TBlogAuthor, TBlogPost, TSettings>>();
    
            await blogManager.LoadBlogSettings();
            await blogManager.ParseAndAddPostsAsync();
        };
        
        return ztaticBuilder;
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

        // Update content to copy target path to include Options.OutputFolderPath.
        var contentToCopy = ztaticService.Options.ContentToCopyToOutput.Select(x => x with { TargetPath = Path.Combine(ztaticService.Options.OutputFolderPath, x.TargetPath) }).ToList();
        ztaticService.Options.ContentToCopyToOutput.Clear();
        ztaticService.Options.ContentToCopyToOutput.AddRange(contentToCopy);
        
        var assets = StaticWebAssetsFinder.FindDefaultStaticAssets(ztaticService.Options.OutputFolderPath, app.Environment.WebRootFileProvider);
        ztaticService.Options.ContentToCopyToOutput.AddRange(assets);
        
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Register(async void () =>
            {
                // Clear and create output folder.
                if (Directory.Exists(ztaticService.Options.OutputFolderPath))
                    Directory.Delete(ztaticService.Options.OutputFolderPath, true);
                Directory.CreateDirectory(ztaticService.Options.OutputFolderPath);
                
                if (ztaticService.Options.BeforeContentGeneratedAction is not null)
                    await ztaticService.Options.BeforeContentGeneratedAction.Invoke(app.Services, ztaticService.Options);
                
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

public sealed record ZtaticBuilder(IServiceCollection Services, ZtaticOptions Options);