namespace Ztatic.Pipelines;

public delegate Task ContentMiddlewareDelegate(ContentContext context);

public sealed class ContentPipeline
{
    private readonly List<Func<ContentMiddlewareDelegate, ContentMiddlewareDelegate>> middlewares = [];
    
    public ContentPipeline Use(Func<ContentContext, ContentMiddlewareDelegate, Task> middleware)
    {
        middlewares.Add(next => context => middleware(context, next));
        return this;
    }
    
    internal ContentMiddlewareDelegate Build()
    {
        ContentMiddlewareDelegate app = context => Task.CompletedTask; // Terminal delegate

        foreach (var component in middlewares.AsEnumerable().Reverse())
        {
            app = component(app);
        }

        return app;
    }
}

public class ContentContext
{
    public required IServiceProvider Services { get; init; }
    public required ZtaticOptions Options { get; init; }
    public required string SourcePath { get; set; }
    public required string TargetPath { get; set; }
    public required Stream Content { get; set; }
}