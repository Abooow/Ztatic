using System.Text.Json;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;

namespace Ztatic.Blogs;

public sealed class BlogManager(BlogConfigOptions config, ILogger<BlogManager> logger) : BlogManager<BlogInfo, BlogAuthor, BlogPost, BlogSettings<BlogAuthor>>(config, logger)
{
}

public interface IBlogManager<TBlogInfo, out TBlogAuthor, out TBlogPost, out TSettings>
    where TBlogInfo : BlogInfo, new()
    where TBlogAuthor : BlogAuthor, new()
    where TBlogPost : BlogPost<TBlogInfo, TBlogAuthor>, new()
    where TSettings : BlogSettings<TBlogAuthor>, new()
{
    TSettings Settings { get; }
    
    Task LoadBlogSettingsAsync(string settingsPath);
    Task ParseAndAddPostsAsync(string postsPath, string postFilePattern = "*.md");
    void Clear();
    
    TBlogPost AddPost(string htmlContent, TBlogInfo blogInfo, List<PostHeader> headers, string? file = null);
    bool UpdatePost(string id, string htmlContent, TBlogInfo blogInfo, List<PostHeader> headers, string? file = null);
    bool RemovePost(string id);
    
    TBlogPost? TryGetBlogPost(string id);
    TBlogPost? TryGetBlogPostByFile(string file);

    IEnumerable<TBlogPost> GetBlogPosts(bool includeDrafts = true);
    IEnumerable<TBlogPost> GetBlogPostsByTag(string tag);
    IEnumerable<TBlogPost> GetBlogPostsByTags(string[] tags, bool includeDrafts = true);
    IEnumerable<TBlogAuthor> GetAuthors();
    IEnumerable<BlogTag> GetTags();
    int CountBlogPosts(bool includeDrafts = true);
    int CountBlogPostsByTags(string[] tags, bool includeDrafts = true);
    
    Task<string> ParseMarkdownFileContentAsync(string filePath);
    Task<(string HtmlContent, TBlogInfo BlogInfo, List<PostHeader> Headers)> ParseMarkdownFileAsync(string filePath, IDeserializer? frontMatterDeserializer = null);
    Task<(string HtmlContent, T BlogInfo, List<PostHeader> Headers)> ParseMarkdownFileAsync<T>(string filePath, IDeserializer? frontMatterDeserializer = null)
        where T : BlogInfo, new();
}

public class BlogManager<TBlogInfo, TBlogAuthor, TBlogPost, TSettings>(BlogConfigOptions config, ILogger<BlogManager<TBlogInfo, TBlogAuthor, TBlogPost, TSettings>> logger)
    : IBlogManager<TBlogInfo, TBlogAuthor, TBlogPost, TSettings>
    where TBlogInfo : BlogInfo, new()
    where TBlogAuthor : BlogAuthor, new()
    where TBlogPost : BlogPost<TBlogInfo, TBlogAuthor>, new()
    where TSettings : BlogSettings<TBlogAuthor>, new()
{
    public TSettings Settings { get; private set; } = new();
    
    private readonly Dictionary<string, TBlogPost> posts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TBlogAuthor> authors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BlogTag> tags = new(StringComparer.OrdinalIgnoreCase);

    public async Task LoadBlogSettingsAsync(string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            logger.LogWarning("The blog settings path '{Path}' does not exist.", settingsPath);
            return;
        }
        
        var json = await File.ReadAllTextAsync(settingsPath);
        try
        {
            Settings = JsonSerializer.Deserialize<TSettings>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to deserialize blog settings file '{Path}' to type '{Type}'", settingsPath, typeof(TSettings));
            return;
        }

        foreach (var author in Settings.Authors)
        {
            authors[author.Id] = author;
        }
        
        foreach (var tag in Settings.Tags)
        {
            tags[tag.Id] = tag;
        }
    }
    
    public async Task ParseAndAddPostsAsync(string postsPath, string postFilePattern = "*.md")
    {
        if (!Directory.Exists(postsPath))
        {
            logger.LogWarning("The blogs directory path '{Path}' does not exist.", postsPath);
            return;
        }
        
        EnumerationOptions enumerationOptions = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true
        };

        var files = Directory.GetFiles(postsPath, postFilePattern, enumerationOptions);
        foreach (var file in files)
        {
            var (htmlContent, blogInfo, headers) = await ParseMarkdownFileAsync<TBlogInfo>(file);
            AddPost(htmlContent, blogInfo, headers, file);
        }
    }

    public void Clear()
    {
        posts.Clear();
        authors.Clear();
        tags.Clear();
    }
    
    public TBlogPost AddPost(string htmlContent, TBlogInfo blogInfo, List<PostHeader> headers, string? file = null)
    {
        var post = new TBlogPost()
        {
            HtmlContent = htmlContent,
            Info = blogInfo,
            File = file,
            Headers = headers,
            Authors = blogInfo.Authors.Select(GetOrCreateBlogAuthor).ToList(),
            Tags = blogInfo.Tags.Select(GetOrCreateBlogTag).ToList()
        };
        
        if (posts.ContainsKey(blogInfo.Id))
            logger.LogWarning("A blog post with id {Id} already exists and will be overwritten.", blogInfo.Id);
        
        return posts[blogInfo.Id] = post;
    }

    public bool UpdatePost(string id, string htmlContent, TBlogInfo blogInfo, List<PostHeader> headers, string? file = null)
    {
        if (!posts.TryGetValue(id, out var post))
            return false;
        
        if (id != blogInfo.Id)
        {
            posts.Remove(id);
            posts.Add(blogInfo.Id, post);
        }
        
        post.Info = blogInfo;
        post.HtmlContent = htmlContent;
        post.Headers = headers;
        post.Authors = blogInfo.Authors.Select(GetOrCreateBlogAuthor).ToList();
        post.Tags = blogInfo.Tags.Select(GetOrCreateBlogTag).ToList();
        post.File = file ?? post.File;
        
        return true;
    }

    public bool RemovePost(string id)
    {
        return posts.Remove(id);
    }
    
    private TBlogAuthor GetOrCreateBlogAuthor(string authorId)
    {
        if (authors.TryGetValue(authorId, out var author))
            return author;
        
        return authors[authorId] = new TBlogAuthor() { Name = authorId };
    }
    
    private BlogTag GetOrCreateBlogTag(string tagId)
    {
        if (tags.TryGetValue(tagId, out var tag))
            return tag;
        
        return tags[tagId] = new BlogTag() { Name = tagId };
    }

    public TBlogPost? TryGetBlogPost(string id)
    {
        posts.TryGetValue(id, out var post);
        return post;
    }
    
    public TBlogPost? TryGetBlogPostByFile(string file)
    {
        return posts.Values.FirstOrDefault(x => x.File == file);
    }
    
    public IEnumerable<TBlogPost> GetBlogPosts(bool includeDrafts = true)
    {
        return posts.Values
            .Where(x => !x.Info.Draft || includeDrafts)
            .OrderByDescending(x => x.Info.Published);
    }

    public IEnumerable<TBlogPost> GetBlogPostsByTag(string tag)
    {
        return posts.Values
            .Where(post => post.Tags.Any(postTag => postTag.Id.Equals(tag, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x => x.Info.Published);
    }
    
    public IEnumerable<TBlogPost> GetBlogPostsByTags(string[] tags, bool includeDrafts = true)
    {
        return posts.Values
            .Where(x => !x.Info.Draft || includeDrafts)
            .Where(post => tags.All(tagId => post.Tags.Any(postTag => postTag.Id.Equals(tagId, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(x => x.Info.Published);
    }
    
    public IEnumerable<TBlogAuthor> GetAuthors()
    {
        return authors.Values;
    }
    
    public IEnumerable<BlogTag> GetTags()
    {
        return tags.Values;
    }
    
    public int CountBlogPosts(bool includeDrafts = true)
    {
        return posts.Values.Count(x => !x.Info.Draft || includeDrafts);
    }
    
    public int CountBlogPostsByTags(string[] tags, bool includeDrafts = true)
    {
        return posts.Values
            .Where(x => !x.Info.Draft || includeDrafts)
            .Count(post => tags.All(tagId => post.Tags.Any(postTag => postTag.Id.Equals(tagId, StringComparison.OrdinalIgnoreCase))));
    }
    
    public async Task<string> ParseMarkdownFileContentAsync(string filePath)
    {
        var markdownContent = await File.ReadAllTextAsync(filePath);
        var htmlContent = Markdown.ToHtml(markdownContent, config.MarkdownPipeline);
        return htmlContent;
    }

    public Task<(string HtmlContent, TBlogInfo BlogInfo, List<PostHeader> Headers)> ParseMarkdownFileAsync(string filePath, IDeserializer? frontMatterDeserializer = null)
    {
        return ParseMarkdownFileAsync<TBlogInfo>(filePath, frontMatterDeserializer);
    }
    
    public async Task<(string HtmlContent, T BlogInfo, List<PostHeader> Headers)> ParseMarkdownFileAsync<T>(string filePath, IDeserializer? frontMatterDeserializer = null)
        where T : BlogInfo, new()
    {
        frontMatterDeserializer ??= config.FrontMatterDeserializer;
        var markdownContent = await File.ReadAllTextAsync(filePath);
        var document = Markdown.Parse(markdownContent, config.MarkdownPipeline);

        var yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
        T blogInfo;
        if (yamlBlock is null)
        {
            blogInfo = new T();
        }
        else
        {
            var frontMatterYaml = yamlBlock.Lines.ToString();

            try
            {
                blogInfo = frontMatterDeserializer.Deserialize<T>(frontMatterYaml);
            }
            catch(Exception e)
            {
                blogInfo = new T();
                logger.LogWarning("Cannot deserialize YAML front matter in {FilePath}. The default one will be used! Error: {ExceptionMessage}", filePath, e.Message + e.InnerException?.Message);
            }
        }
        
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        blogInfo.Id = !string.IsNullOrWhiteSpace(blogInfo.Id) ? blogInfo.Id : fileName;
        blogInfo.ThumbnailAltText = !string.IsNullOrWhiteSpace(blogInfo.ThumbnailAltText) ? blogInfo.ThumbnailAltText : blogInfo.Title;
        
        var contentWithoutFrontMatter = markdownContent[(yamlBlock == null ? 0 : yamlBlock.Span.End + 1)..];
        var htmlContent = Markdown.ToHtml(contentWithoutFrontMatter, config.MarkdownPipeline);
        var contentDocument = Markdown.Parse(contentWithoutFrontMatter, config.MarkdownPipeline);

        var headers = contentDocument.Descendants<HeadingBlock>()
            .Select(x => new PostHeader(x.Inline?.FirstChild?.ToString() ?? "", x.GetAttributes().Id!, (HeadingLevel)x.Level))
            .ToList();
        
        return (htmlContent, blogInfo, headers);
    }
}