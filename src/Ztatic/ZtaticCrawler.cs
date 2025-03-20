using System.Net;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;

namespace Ztatic;

internal sealed class ZtaticCrawler(string appUrl, ZtaticOptions options, ILogger logger)
{
    private readonly HashSet<string> savedPathsSet = [];
    private readonly HttpClient httpClient = new();
    private readonly HtmlParser htmlParser = new();
    
    internal async Task StartCrawlingAsync()
    {
        await CrawlPageAsync("/");

        foreach (var url in options.ExplicitUrlsToFetch)
        {
            await CrawlPageAsync(url);
        }
    }

    private Task CrawlPageAsync(string path)
    {
        return CrawlPageAsync((Href: $"about://{path}", Protocol: "about:", PathName: path));
    }
    
    private async Task CrawlPageAsync((string Href, string Protocol, string PathName) args)
    {
        var href = args.Href.Split('#').FirstOrDefault() ?? "";
        if (!savedPathsSet.Add(href))
            return;
        
        if (args.Protocol is not "about:")
        {
            logger.LogWarning("The requested URL ({Href}) was not navigable.", args.Href);
            return;
        }
        
        var requestUrl = appUrl + args.PathName;
        logger.LogInformation("Getting {RequestUrl}", requestUrl);
        
        if (!Uri.TryCreate(requestUrl, UriKind.Absolute, out var _))
        {
            logger.LogWarning("The requested URL ({RequestUrl}) was not valid format.", requestUrl);
            return;
        }
        
        var response = await httpClient.GetAsync(requestUrl);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            logger.LogWarning("The HTTP status code was not OK. Responded with {StatusCode}.", response.StatusCode);
            return;
        }
        
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not "text/html")
        {
            logger.LogInformation("Expected text/html content type, but was {MediaType}.", mediaType);
            return;
        }
        
        var htmlContent = await response.Content.ReadAsStringAsync();
        var outputPath = GetOutputPath(args.PathName);
        
        await File.WriteAllTextAsync(outputPath, htmlContent);
        
        // Find all anchor elements and crawl them.
        using var htmlDoc = htmlParser.ParseDocument(htmlContent);
        var links = htmlDoc.Links
            .OfType<IHtmlAnchorElement>()
            .Where(link => string.IsNullOrEmpty(link.Origin))
            .Where(link => string.IsNullOrEmpty(link.Target))
            .Where(link => !string.IsNullOrEmpty(link.PathName))
            .ToArray();

        foreach (var link in links)
        {
            await CrawlPageAsync((link.Href, link.Protocol, link.PathName));
        }
    }
    
    private string GetOutputPath(string path)
    {
        string? outFilePath = null;
        if (options.PageOutputStyle is OutputStyle.IndexHtmlInSubFolders)
        {
            outFilePath = Path.Combine(options.OutputFolderPath, Path.Combine(path.Split('/')), options.HtmlIndexPageName);
        }
        else if (options.PageOutputStyle is OutputStyle.AppendHtmlExtension)
        {
            outFilePath = path is "" or "/" ?
                Path.Combine(options.OutputFolderPath, options.HtmlIndexPageName) :
                Path.Combine(options.OutputFolderPath, Path.Combine(path.Split('/'))) + ".html";
        }
        
        if (outFilePath is null)
            throw new NullReferenceException();

        var targetDir = Path.GetDirectoryName(outFilePath);
        if (string.IsNullOrEmpty(targetDir))
            throw new NullReferenceException();

        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);

        return outFilePath;
    }
}