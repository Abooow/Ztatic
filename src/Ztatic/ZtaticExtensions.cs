using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
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
        
        AddDefaultStaticWebAssetsToOutput(app.Environment.WebRootFileProvider, string.Empty, ztaticService.Options);
        
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Register(async void () =>
            {
                try
                {
                    await ztaticService.GenerateStaticPagesAsync(app.Urls.First()).ConfigureAwait(false);
                    
                    if(shutdownApp)
                        lifetime.StopApplication();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while generating static pages: {ErrorMessage}", ex.Message);
                }
            }
        );
    }
    
    private static void AddDefaultStaticWebAssetsToOutput(IFileProvider fileProvider, string subPath, ZtaticOptions ztaticOptions)
    {
        var contents = fileProvider.GetDirectoryContents(subPath);

        foreach(var item in contents)
        {
            var fullPath = $"{subPath}{item.Name}";
            if(item.IsDirectory)
            {
                AddDefaultStaticWebAssetsToOutput(fileProvider, $"{fullPath}/", ztaticOptions);
            }
            else
            {
                if(item.PhysicalPath is not null)
                {
                    ztaticOptions.ContentToCopyToOutput.Add(new ContentToCopy(item.PhysicalPath, fullPath));
                }
            }
        }
    }
}