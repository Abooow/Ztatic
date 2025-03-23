using Microsoft.Extensions.Logging;
using Ztatic.Pipelines;

namespace Ztatic;

internal sealed class ZtaticService(ZtaticOptions options, DiscoveredRoutes discoveredRoutes, ILogger<ZtaticService> logger, IServiceProvider services)
{
    public ZtaticOptions Options => options;
    
    private readonly ContentMiddlewareDelegate contentMiddleware = (options.ContentPipeline ?? new ContentPipeline().CreateFiles()).Build();
    
    public async Task GenerateStaticPagesAsync(string appUrl)
    {
        var crawler = new ZtaticCrawler(appUrl, options, discoveredRoutes, logger, services);
        await crawler.StartCrawlingAsync();
    }

    public async Task CopyAssetsToOutputAsync()
    {
        var uniqueItemsToCopy = options.ContentToCopyToOutput.DistinctBy(x => (x.SourcePath, x.TargetPath));
        var ignoredPathsWithOutputFolder = options.IgnoredPathsOnContentCopy.Select(x => Path.Combine(options.OutputFolderPath, x)).ToList();
        
        foreach(var itemToCopy in uniqueItemsToCopy)
        {
            var targetPath = Path.Combine(options.OutputFolderPath, itemToCopy.TargetPath);
            await CopyContentAsync(itemToCopy.SourcePath, targetPath, ignoredPathsWithOutputFolder);
        }
    }
    
    private async Task CopyContentAsync(string sourcePath, string targetPath, List<string> ignoredPaths)
    {
        if (ignoredPaths.Contains(targetPath))
            return;

        // Source path is a file.
        if (File.Exists(sourcePath))
        {
            var dir = Path.GetDirectoryName(targetPath);
            if (dir is null)
                return;

            await using var stream = File.OpenRead(sourcePath);
            await contentMiddleware.Invoke(new ContentContext()
            {
                Services = services,
                Options = options,
                SourcePath = sourcePath,
                TargetPath = targetPath,
                Content = stream
            });
            
            return;
        }

        if (!Directory.Exists(sourcePath))
        {
            logger.LogError("The directory '{SourcePath}' does not exist", sourcePath);
            return;
        }

        var ignoredPathsWithTarget = ignoredPaths.Select(x => Path.Combine(targetPath, x)).ToList();

        // Copy all the files from source directory.
        foreach (var newSourcePath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
        {
            var newTargetPath = ChangeRootFolder(newSourcePath);
            if (ignoredPathsWithTarget.Contains(newTargetPath))
                continue;

            await using var stream = File.OpenRead(newSourcePath);
            await contentMiddleware.Invoke(new ContentContext()
            {
                Services = services,
                Options = options,
                SourcePath = newSourcePath,
                TargetPath = newTargetPath,
                Content = stream
            });
        }

        return;

        // For example  from "wwwroot/imgs" to "output/imgs" (safer string.Replace)
        string ChangeRootFolder(string dirPath)
        {
            var relativePath = dirPath[sourcePath.Length..].TrimStart(Path.DirectorySeparatorChar);
            return Path.Combine(targetPath, relativePath);
        }
    }
}