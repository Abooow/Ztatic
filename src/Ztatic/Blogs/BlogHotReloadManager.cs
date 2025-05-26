using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Ztatic.Blogs;

public sealed class BlogHotReloadManager<TBlogInfo, TBlogAuthor, TBlogPost, TSettings>(BlogConfigOptions options, IBlogManager<TBlogInfo, TBlogAuthor, TBlogPost, TSettings> blogManager, ILogger<BlogHotReloadManager<TBlogInfo, TBlogAuthor, TBlogPost, TSettings>> logger) : IDisposable
    where TBlogInfo : BlogInfo, new()
    where TBlogAuthor : BlogAuthor, new()
    where TBlogPost : BlogPost<TBlogInfo, TBlogAuthor>, new()
    where TSettings : BlogSettings<TBlogAuthor>, new()
{
    private FileSystemWatcher? watcher;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> fileLocks = [];

    public void StartHotReload()
    {
        watcher = new FileSystemWatcher(options.PostsPath);
        watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
        watcher.Filter = options.PostFilePattern;
        watcher.IncludeSubdirectories = true;
        watcher.EnableRaisingEvents = true;
        
        watcher.Changed += WatcherOnChanged;
        watcher.Created += WatcherOnCreated;
        watcher.Renamed += WatcherOnRenamed;
        watcher.Deleted += WatcherOnDeleted;

        logger.LogInformation("Started blog hot reload in directory: {Directory}", options.PostsPath);
    }
    
    private async void WatcherOnChanged(object sender, FileSystemEventArgs args)
    {
        try
        {
            if (args.ChangeType is not WatcherChangeTypes.Changed)
                return;
        
            var post = blogManager.TryGetBlogPostByFile(args.FullPath);
            if (post is null)
                return;

            bool updated = await TryUpdateBlogAsync(post.Info.Id, args.FullPath);
            if (!updated)
                return;
            
            logger.LogInformation("Blog post updated: {FilePath}", args.FullPath);
            
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update blog post: {FilePath}", args.FullPath);
        }
    }
    
    private async void WatcherOnCreated(object sender, FileSystemEventArgs args)
    {
        try
        {
            var (htmlContent, blogInfo, headers) = await blogManager.ParseMarkdownFileAsync(args.FullPath);
            blogManager.AddPost(htmlContent, blogInfo, headers, args.FullPath);
            
            logger.LogInformation("Blog post created: {FilePath}", args.FullPath);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create new blog post: {FilePath}", args.FullPath);
        }
    }

    private async void WatcherOnRenamed(object sender, RenamedEventArgs args)
    {
        try
        {
            var post = blogManager.TryGetBlogPostByFile(args.OldFullPath);
            if (post is null)
                return;

            bool updated = await TryUpdateBlogAsync(post.Info.Id, args.FullPath);
            if (!updated)
                return;
            
            logger.LogInformation("Blog post renamed: {OldFilePath} to {NewFilePath}", args.OldFullPath, args.FullPath);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update blog post: {NewFilePath}", args.FullPath);
        }
    }
    
    private void WatcherOnDeleted(object sender, FileSystemEventArgs args)
    {
        try
        {
            var post = blogManager.TryGetBlogPostByFile(args.FullPath);
            if (post is not null && blogManager.RemovePost(post.Info.Id))
                logger.LogInformation("Blog post deleted: {FilePath}", args.FullPath);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to delete blog post: {FilePath}", args.FullPath);
        }
    }

    public void Dispose()
    {
        watcher?.Dispose();
    }
    
    private async Task<bool> TryUpdateBlogAsync(string postId, string filePath)
    {
        var fileLock = fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
        if (!await fileLock.WaitAsync(0))
            return false;

        try
        {
            await Task.Delay(10);
            
            var (htmlContent, blogInfo, headers) = await blogManager.ParseMarkdownFileAsync(filePath);
            if (!blogManager.UpdatePost(postId, htmlContent, blogInfo, headers, filePath))
                return false;
            
            return true;
        }
        finally
        {
            fileLock.Release();
            fileLocks.TryRemove(filePath, out _);
        }
    }
}