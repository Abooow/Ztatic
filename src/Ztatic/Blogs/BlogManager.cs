using System.Text.Json;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;

namespace Ztatic.Blogs;

public sealed class BlogManager(BlogConfigOptions config, ILogger<BlogManager> logger) : BlogManager<BlogInfo, BlogAuthor, BlogPost, BlogSettings<BlogAuthor>>(config, logger)
{
}

public interface IBlogManager<in TBlogInfo, out TBlogAuthor, out TBlogPost, out TSettings>
    where TBlogInfo : BlogInfo, new()
    where TBlogAuthor : BlogAuthor, new()
    where TBlogPost : BlogPost<TBlogInfo, TBlogAuthor>, new()
    where TSettings : BlogSettings<TBlogAuthor>, new()
{
    TSettings Settings { get; }
    
    Task LoadBlogSettings();
    Task ParseAndAddPostsAsync();
    void Clear();
    TBlogPost AddPost(string htmlContent, TBlogInfo blogInfo);
    
    TBlogPost? TryGetBlogPost(string id);
    IEnumerable<TBlogPost> GetBlogPosts();
    IEnumerable<TBlogAuthor> GetAuthors();
    IEnumerable<BlogTag> GetTags();
}

public class BlogManager<TBlogInfo, TBlogAuthor, TBlogPost, TSettings>(BlogConfigOptions config, ILogger<BlogManager<TBlogInfo, TBlogAuthor, TBlogPost, TSettings>> logger)
    : IBlogManager<TBlogInfo, TBlogAuthor, TBlogPost, TSettings>
    where TBlogInfo : BlogInfo, new()
    where TBlogAuthor : BlogAuthor, new()
    where TBlogPost : BlogPost<TBlogInfo, TBlogAuthor>, new()
    where TSettings : BlogSettings<TBlogAuthor>, new()
{
    public TSettings Settings { get; private set; } = new();
    
    private readonly Dictionary<string, TBlogPost> posts = [];
    private readonly Dictionary<string, TBlogAuthor> authors = [];
    private readonly Dictionary<string, BlogTag> tags = [];

    public async Task LoadBlogSettings()
    {
        if (!File.Exists(config.SettingsPath))
        {
            logger.LogWarning("The blog settings path '{Path}' does not exist.", config.SettingsPath);
            return;
        }
        
        var json = await File.ReadAllTextAsync(config.SettingsPath);
        try
        {
            Settings = JsonSerializer.Deserialize<TSettings>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        }
        catch (Exception e)
        {
            logger.LogWarning("Failed to deserialize blog settings file '{Path}' to type '{Type}'. Message: {Error}", config.SettingsPath, typeof(TSettings), e.Message);
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
    
    public async Task ParseAndAddPostsAsync()
    {
        if (!Directory.Exists(config.PostsPath))
        {
            logger.LogWarning("The blogs directory path '{Path}' does not exist.", config.PostsPath);
            return;
        }
        
        EnumerationOptions enumerationOptions = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true
        };

        var files = Directory.GetFiles(config.PostsPath, config.PostFilePattern, enumerationOptions);
        foreach (var file in files)
        {
            var (htmlContent, blogInfo) = await ParseMarkdownFileAsync<TBlogInfo>(file);
            AddPost(htmlContent, blogInfo);
        }
    }

    public void Clear()
    {
        posts.Clear();
        authors.Clear();
        tags.Clear();
    }
    
    public TBlogPost AddPost(string htmlContent, TBlogInfo blogInfo)
    {
        var post = new TBlogPost()
        {
            HtmlContent = htmlContent,
            Info = blogInfo,
            Authors = blogInfo.Authors.Select(GetOrCreateBlogAuthor).ToList(),
            Tags = blogInfo.Tags.Select(GetOrCreateBlogTag).ToList()
        };
        
        if (posts.ContainsKey(blogInfo.Id))
            logger.LogWarning("A blog post with id {Id} already exists and will be overwritten.", blogInfo.Id);
        
        return posts[blogInfo.Id] = post;
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
    
    public IEnumerable<TBlogPost> GetBlogPosts()
    {
        return posts.Values;
    }
    
    public IEnumerable<TBlogAuthor> GetAuthors()
    {
        return authors.Values;
    }
    
    public IEnumerable<BlogTag> GetTags()
    {
        return tags.Values;
    }
    
    public async Task<string> ParseMarkdownFileAsync(string filePath)
    {
        var markdownContent = await File.ReadAllTextAsync(filePath);
        var htmlContent = Markdown.ToHtml(markdownContent, config.MarkdownPipeline);
        return htmlContent;
    }
    
    public async Task<(string HtmlContent, T BlogInfo)> ParseMarkdownFileAsync<T>(string filePath, IDeserializer? frontMatterDeserializer = null)
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
                logger.LogWarning("Cannot deserialize YAML front matter in {file}. The default one will be used! Error: {exceptionMessage}", filePath, e.Message + e.InnerException?.Message);
            }
        }
        
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        blogInfo.Slug = (new[] { blogInfo.Slug, blogInfo.Id, fileName }).First(x => !string.IsNullOrWhiteSpace(x));
        blogInfo.Id = !string.IsNullOrWhiteSpace(blogInfo.Id) ? blogInfo.Id : blogInfo.Slug;
        blogInfo.ThumbnailAltText = !string.IsNullOrWhiteSpace(blogInfo.ThumbnailAltText) ? blogInfo.ThumbnailAltText : blogInfo.Title;
        
        var contentWithoutFrontMatter = markdownContent[(yamlBlock == null ? 0 : yamlBlock.Span.End + 1)..];
        var htmlContent = Markdown.ToHtml(contentWithoutFrontMatter, config.MarkdownPipeline);
        return (htmlContent, blogInfo);
    }
}