using Markdig;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Ztatic.Blogs;

public sealed class BlogConfigOptions
{
    public string PostsPath { get; set; } = "Blogs";
    
    public string PostFilePattern { get; set; } = "*.md";
    
    public string SettingsPath { get; set; } = "Blogs/blogsettings.json";

    public bool EnableHotReload { get; set; }
    
    public MarkdownPipeline MarkdownPipeline { get; set; } = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseAutoIdentifiers()
        .UseYamlFrontMatter()
        .Build();
    
    public IDeserializer FrontMatterDeserializer { get; set; } = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();
}