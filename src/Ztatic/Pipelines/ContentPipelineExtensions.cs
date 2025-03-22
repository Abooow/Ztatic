using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        });
        
        return pipeline;
    }
}