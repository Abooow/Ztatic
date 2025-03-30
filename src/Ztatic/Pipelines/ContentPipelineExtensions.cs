using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebMarkupMin.Core;
using Ztatic.Pipelines;

namespace Ztatic;

public static class ContentPipelineExtensions
{
    public static ContentPipeline CreateFiles(this ContentPipeline pipeline)
    {
        pipeline.Use(async (ctx, next) =>
        {
            await next(ctx);

            var targetDir = Path.GetDirectoryName(ctx.TargetPath);
            if (string.IsNullOrEmpty(targetDir))
                throw new NullReferenceException();

            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);
            
            var logger = ctx.Services.GetRequiredService<ILogger<ContentPipeline>>();
            logger.LogInformation("Copying {SourcePath} to {TargetPath}", ctx.SourcePath, ctx.TargetPath);
            
            ctx.Content.Position = 0;
            await using var file = File.Create(ctx.TargetPath);
            await ctx.Content.CopyToAsync(file);
            ctx.Content.Position = 0;
        });
        
        return pipeline;
    }

    /// <param name="extensionsToMinify">Default extensions is [".html", ".css", ".js"]</param>
    public static ContentPipeline MinifyContent(this ContentPipeline pipeline, string[]? extensionsToMinify = null)
    {
        pipeline.Use(async (ctx, next) =>
        {
            await next(ctx);

            var extension = Path.GetExtension(ctx.TargetPath).ToLower();
            var extensions = extensionsToMinify ?? [".html", ".css", ".js"];
            if (extension is not (".html" or ".css" or ".js") || !extensions.Contains(extension))
                return;
            
            var logger = ctx.Services.GetRequiredService<ILogger<ContentPipeline>>();
            logger.LogInformation("Minifying {TargetPath}", ctx.TargetPath);
            
            using var reader = new StreamReader(ctx.Content, leaveOpen: true);
            string originalContent = await reader.ReadToEndAsync();

            var result = MinifyContent(originalContent, extension);
            if (result.ErrorMessage is not null)
            {
                logger.LogWarning("Minification errors for {TargetPath}: {Errors}", ctx.TargetPath, result.ErrorMessage);
                return;
            }
            
            // Replace the content stream with the minified version.
            await ctx.Content.DisposeAsync();
            ctx.Content = new MemoryStream();

            await using var writer = new StreamWriter(ctx.Content, leaveOpen: true);
            await writer.WriteAsync(result.MinifiedContent);
            await writer.FlushAsync();
            ctx.Content.Position = 0;
        });
        
        return pipeline;
    }

    private static (string MinifiedContent, string? ErrorMessage) MinifyContent(string originalContent, string extension)
    {
        if (extension is ".html")
        {
            var htmlMinifier = new HtmlMinifier();
            var minifiedResult = htmlMinifier.Minify(originalContent);

            if (minifiedResult.Errors.Count > 0)
                return ("", string.Join("; ", minifiedResult.Errors));

            return (minifiedResult.MinifiedContent, null);
        }

        if (extension is ".css")
        {
            var cssMinifier = new KristensenCssMinifier();
            var minifiedResult = cssMinifier.Minify(originalContent, false);

            if (minifiedResult.Errors.Count > 0)
                return ("", string.Join("; ", minifiedResult.Errors));

            return (minifiedResult.MinifiedContent, null);
        }

        if (extension is ".js")
        {
            var jsMinifier = new CrockfordJsMinifier();
            var minifiedResult = jsMinifier.Minify(originalContent, false);

            if (minifiedResult.Errors.Count > 0)
                return ("", string.Join("; ", minifiedResult.Errors));

            return (minifiedResult.MinifiedContent, null);
        }

        return ("", $"Unsupported extension: {extension}");
    }
    
    /// <param name="extensionsToCompress">Default extensions is [".html", ".css", ".js"]</param>
    public static ContentPipeline CreateGZipCompressedFiles(this ContentPipeline pipeline, string[]? extensionsToCompress = null)
    {
        pipeline.Use(async (ctx, next) =>
        {
            await next(ctx);

            var extension = Path.GetExtension(ctx.TargetPath).ToLower();
            var extensions = extensionsToCompress ?? [".html", ".css", ".js"];
            if (extension is ".gz" || !extensions.Contains(extension))
                return;

            // A compressed version will already be copied.
            var compressedTargetPath = ctx.TargetPath + ".gz";
            if (ctx.Options.ContentToCopyToOutput.Any(x => x.TargetPath == compressedTargetPath))
                return;
            
            var logger = ctx.Services.GetRequiredService<ILogger<ContentPipeline>>();
            logger.LogInformation("Compressing {TargetPath} to {CompressedTargetPath}", ctx.TargetPath, compressedTargetPath);
            
            await using var compressedFileStream = File.Create(compressedTargetPath);
            await using var gzipStream = new GZipStream(compressedFileStream, CompressionLevel.SmallestSize);

            await ctx.Content.CopyToAsync(gzipStream);
            ctx.Content.Position = 0;
        });
        
        return pipeline;
    }
}