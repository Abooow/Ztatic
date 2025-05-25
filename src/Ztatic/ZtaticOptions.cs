using Ztatic.Pipelines;

namespace Ztatic;

public sealed class ZtaticOptions
{
    public bool SuppressFileGeneration { get; set; }
    
    public string OutputFolderPath { get; set; } = default!;
    
    public List<string> ExplicitUrlsToFetch { get; init; } = [];

    public string HtmlIndexPageName { get; set; } = default!;
    
    public OutputStyle PageOutputStyle { get; set; }
    
    public List<ContentToCopy> ContentToCopyToOutput { get; init; } = [];

    public List<string> IgnoredPathsOnContentCopy { get; init; } = [];

    public string? SiteUrl { get; set; }
    
    public Func<IServiceProvider, ZtaticOptions, Task>? BeforeContentGeneratedAction { get; set; }
    
    public Func<IServiceProvider, ZtaticOptions, Task>? AfterContentGeneratedAction { get; set; }
    
    internal ContentPipeline? ContentPipeline { get; set; }
    
    public void ConfigureContentPipeline(Action<ContentPipeline> pipeline)
    {
        ContentPipeline = new();
        pipeline.Invoke(ContentPipeline);
    }
}

public sealed record ContentToCopy(string SourcePath, string TargetPath);

public enum OutputStyle
{
    IndexHtmlInSubFolders,
    AppendHtmlExtension
}
