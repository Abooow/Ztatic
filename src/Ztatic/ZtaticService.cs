using Microsoft.Extensions.Logging;

namespace Ztatic;

internal sealed class ZtaticService(ZtaticOptions options, ILogger<ZtaticService> logger)
{
    public ZtaticOptions Options => options;

    public async Task GenerateStaticPagesAsync(string appUrl)
    {
        // Clear output folder.
        if (Directory.Exists(options.OutputFolderPath))
            Directory.Delete(options.OutputFolderPath, true);
        Directory.CreateDirectory(options.OutputFolderPath);

        // Generate static pages.
        var crawler = new ZtaticCrawler(appUrl, options, logger);
        await crawler.StartCrawlingAsync();
        
        // Copy assets to output.
        var ignoredPathsWithOutputFolder = options.IgnoredPathsOnContentCopy.Select(x => Path.Combine(options.OutputFolderPath, x)).ToList();
        foreach(var pathToCopy in options.ContentToCopyToOutput)
        {
            logger.LogInformation("Copying {sourcePath} to {targetPath}", pathToCopy.SourcePath, Path.Combine(options.OutputFolderPath, pathToCopy.TargetPath));
            CopyContent(pathToCopy.SourcePath, Path.Combine(options.OutputFolderPath, pathToCopy.TargetPath), ignoredPathsWithOutputFolder);
        }
    }
    
    private void CopyContent(string sourcePath, string targetPath, List<string> ignoredPaths)
    {
        if (ignoredPaths.Contains(targetPath))
            return;

        // Source path is a file.
        if (File.Exists(sourcePath))
        {
            var dir = Path.GetDirectoryName(targetPath);
            if(dir is null)
                return;

            Directory.CreateDirectory(dir);
            File.Copy(sourcePath, targetPath, true);
            return;
        }

        if (!Directory.Exists(sourcePath))
        {
            logger.LogError("Source path ({sourcePath}) does not exist", sourcePath);
            return;
        }

        if (!Directory.Exists(targetPath))
            Directory.CreateDirectory(targetPath);

        var ignoredPathsWithTarget = ignoredPaths.Select(x => Path.Combine(targetPath, x)).ToList();

        // Now Create all the directories.
        foreach (var dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            var newDirPath = ChangeRootFolder(dirPath);
            if(ignoredPathsWithTarget.Contains(newDirPath)) // Folder is mentioned in ignoredPaths, don't create it.
                continue;

            Directory.CreateDirectory(newDirPath);
        }

        // Copy all the files & Replaces any files with the same name.
        foreach (var newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
        {
            var newPathWithNewDir = ChangeRootFolder(newPath);
            if (ignoredPathsWithTarget.Contains(newPathWithNewDir) || !Directory.Exists(Path.GetDirectoryName(newPathWithNewDir))) // Folder where this file resides is mentioned in ignoredPaths.
                continue;

            File.Copy(newPath, newPathWithNewDir, true);
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