namespace Ztatic.Blogs;

public class BlogSettings<TBlogAuthor>
    where TBlogAuthor : BlogAuthor, new()
{
    public List<TBlogAuthor> Authors { get; set; } = [];
    public List<BlogTag> Tags { get; set; } = [];
}

public sealed class BlogPost : BlogPost<BlogInfo, BlogAuthor>
{
}

public class BlogPost<TBlogInfo, TBlogAuthor>
    where TBlogInfo : BlogInfo, new()
    where TBlogAuthor : BlogAuthor, new()
{
    public TBlogInfo Info { get; set; }

    public string HtmlContent { get; set; } = "";

    public string? File { get; set; }

    public List<TBlogAuthor> Authors { get; set; } = [];
    
    public List<BlogTag> Tags { get; set; } = [];
}

public class BlogInfo
{
    public string Id { get; set; }

    public bool Draft { get; set; }
    
    public string Title { get; set; } = "Empty Title";
    
    public string? Description { get; set; }
    
    public int ReadTimeMinutes { get; set; }
    
    public List<string> Authors { get; set; } = [];
    
    public List<string> Tags { get; set; } = [];
    
    public string ThumbnailAltText { get; set; }

    public BlogThumbnails ThumbnailUrls { get; set; } = new();
    
    public DateTime Published { get; set; }
    
    public DateTime? LastModified { get; set; }
}

public class BlogThumbnails
{
    public string? Large { get; set; }
    public string? Medium { get; set; }
    public string? Small { get; set; }

    public bool HasThumbnail => !string.IsNullOrWhiteSpace(Large) && !string.IsNullOrWhiteSpace(Medium) && !string.IsNullOrWhiteSpace(Small);
    
    public BlogThumbnails()
    {
        var large = GetFirstNonEmptyString([Large, Medium, Small], Large);
        var medium = GetFirstNonEmptyString([Medium, Large, Small], Medium);
        var small = GetFirstNonEmptyString([Small, Medium, Large], Small);
        Large = large;
        Medium = medium;
        Small = small;
    }
    
    private static string? GetFirstNonEmptyString(string?[] values, string? defaultValue)
    {
        var firstNonEmpty = values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        if (firstNonEmpty is not null)
            return firstNonEmpty;
        
        return defaultValue;
    }
}

public class BlogAuthor
{
    public string Id { get => field ?? Name; set; }
    public string Name { get; set; }
    public string? ProfileImage { get; set; }
}

public class BlogTag
{
    public string Id { get => field ?? Name; set; }
    public string Name { get; set; }
}