using Ztatic.Pipelines;

namespace Ztatic;

public sealed class ZtaticOptions
{
    public string OutputFolderPath { get; set; } = "output";
    
    public List<string> ExplicitUrlsToFetch { get; } = [];
    
    public string HtmlIndexPageName { get; set; } = "index.html";
    
    public OutputStyle PageOutputStyle { get; set; } = OutputStyle.IndexHtmlInSubFolders;
    
    public List<ContentToCopy> ContentToCopyToOutput { get; } = [];

    public List<string> IgnoredPathsOnContentCopy { get; } = [];

    public string? SiteUrl { get; set; }
    
    public Func<IServiceProvider, ZtaticOptions, Task>? AfterContentGeneratedAction { get; set; }
    
    public bool SuppressFileGeneration { get; set; }
    
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
