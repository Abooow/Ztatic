namespace Ztatic;

public sealed class ZtaticOptions
{
    public string OutputFolderPath { get; set; } = "output";
    
    public List<string> ExplicitUrlsToFetch { get; } = [];
    
    public string HtmlIndexPageName { get; set; } = "index.html";
    
    public OutputStyle PageOutputStyle { get; set; } = OutputStyle.IndexHtmlInSubFolders;
    
    public List<ContentToCopy> ContentToCopyToOutput { get; } = [];

    public List<string> IgnoredPathsOnContentCopy { get; } = [];

    public bool SuppressFileGeneration { get; set; }
}

public sealed record ContentToCopy(string SourcePath, string TargetPath);

public enum OutputStyle
{
    IndexHtmlInSubFolders,
    AppendHtmlExtension
}
