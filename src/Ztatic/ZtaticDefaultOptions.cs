namespace Ztatic;

public sealed class ZtaticDefaultOptions
{
    public bool SuppressFileGeneration { get; set; }
    
    public string OutputFolderPath { get; set; } = "output";
    
    public List<string> ExplicitUrlsToFetch { get; } = [];
    
    public string HtmlIndexPageName { get; set; } = "index.html";
    
    public OutputStyle PageOutputStyle { get; set; } = OutputStyle.IndexHtmlInSubFolders;
    
    public List<ContentToCopy> ContentToCopyToOutput { get; } = [];

    public List<string> IgnoredPathsOnContentCopy { get; } = [];

    public string? SiteUrl { get; set; }
    
    public bool GenerateSitemap { get; set; }

    public string[]? SitemapIgnoredUrls { get; set; }
    
    public string SitemapOutputPath { get; set; } = "sitemap.xml";

    public ZtaticOptions ToZtaticOptions()
    {
        return new ZtaticOptions()
        {
            SuppressFileGeneration = SuppressFileGeneration,
            OutputFolderPath = OutputFolderPath,
            ExplicitUrlsToFetch =  ExplicitUrlsToFetch,
            HtmlIndexPageName = HtmlIndexPageName,
            PageOutputStyle = PageOutputStyle,
            ContentToCopyToOutput = ContentToCopyToOutput,
            IgnoredPathsOnContentCopy = IgnoredPathsOnContentCopy,
            SiteUrl = SiteUrl
        };
    }
}