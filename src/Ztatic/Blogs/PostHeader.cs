namespace Ztatic.Blogs;

public record PostHeader(string Text, string Id, HeadingLevel Level);

public enum HeadingLevel
{
    H1 = 1,
    H2 = 2,
    H3 = 3,
    H4 = 4,
    H5 = 5,
    H6 = 6
}