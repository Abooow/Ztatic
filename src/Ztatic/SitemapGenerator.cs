using System.Text;
using System.Web;

namespace Ztatic;

public static class SitemapGenerator
{
    public static async Task GenerateSitemapAsync(string siteUrl, IEnumerable<DiscoveredRoute> routes, string outputPath)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        builder.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        foreach (var route in routes)
        {
            var pageUrl = siteUrl.TrimEnd('/') + EncodeUrl(route.Url);
            builder.AppendLine("  <url>");
            builder.AppendLine($"    <loc>{pageUrl}</loc>");
            if (route.Info.LastModified.HasValue)
                builder.AppendLine($"    <lastmod>{route.Info.LastModified:yyyy-MM-dd}</lastmod>");
            builder.AppendLine("  </url>");
        }
        
        builder.AppendLine("</urlset>");
        
        var sitemap = builder.ToString();
        await File.WriteAllTextAsync(outputPath, sitemap);
    }
    
    private static string EncodeUrl(string url)
    {
        var encodedUrl = HttpUtility.UrlEncode(url, Encoding.UTF8).Replace("%2f", "/");
        return encodedUrl.StartsWith('/') ? encodedUrl : '/' + encodedUrl;
    }
}