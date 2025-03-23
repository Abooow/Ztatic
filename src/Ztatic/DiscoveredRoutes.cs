namespace Ztatic;

public sealed class DiscoveredRoutes
{
    private readonly Dictionary<string, DiscoveredRoute> discoveredRoutes = new(StringComparer.InvariantCultureIgnoreCase);
    private readonly Dictionary<string, RouteInfo> routeInfos = new(StringComparer.InvariantCultureIgnoreCase);
    
    public DiscoveredRoute AddRoute(string url, RouteInfo? info = null)
    {
        var routeInfo = info ?? new RouteInfo();
        if (routeInfos.TryGetValue(url, out var existingRouteInfo))
        {
            routeInfo = existingRouteInfo;
            routeInfos.Remove(url);
        }

        var route = new DiscoveredRoute() { Url = url, Info = routeInfo };
        discoveredRoutes[url] = route;

        return route;
    }

    public void UpdateRouteInfo(string url, RouteInfo newInfo)
    {
        if (discoveredRoutes.TryGetValue(url, out var existingRoute))
            existingRoute.Info = newInfo;
        else
            routeInfos[url] = newInfo;
    }
    
    public IEnumerable<DiscoveredRoute> GetDiscoveredRoutes()
    {
        return discoveredRoutes.Values;
    }
}

public sealed class DiscoveredRoute
{
    public required string Url { get; init; }
    public required RouteInfo Info { get; set; }
}

public class RouteInfo
{
    public DateTime? LastModified { get; set; }
}