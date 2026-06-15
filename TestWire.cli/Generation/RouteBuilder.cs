using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TestWire.cli.Analysis;

namespace TestWire.cli.Generation;

public static class RouteBuilder
{
    public static string Build(string baseRoute, string className, string? routeSegment, List<ParameterDetail> parameters)
    {
        return BuildInternal(baseRoute, className, routeSegment, parameters, asNotFound: false);
    }

    public static string BuildNotFound(string baseRoute, string className, string? routeSegment, List<ParameterDetail> parameters)
    {
        return BuildInternal(baseRoute, className, routeSegment, parameters, asNotFound: true);
    }

    private static string BuildInternal(string baseRoute, string className, string? routeSegment, List<ParameterDetail> parameters, bool asNotFound)
    {
        var controllerName = ResolveControllerName(className);
        var resolvedBase = baseRoute.Replace("[controller]", controllerName, StringComparison.OrdinalIgnoreCase);
        var combined = CombineSegments(resolvedBase, routeSegment);
        var routeWithTokens = ReplaceRouteTokens(combined, parameters, asNotFound);

        return AppendQueryParameters(routeWithTokens, parameters, asNotFound);
    }

    private static string AppendQueryParameters(string route, List<ParameterDetail> parameters, bool asNotFound)
    {
        var queryParams = parameters.Where(p => p.IsFromQuery).ToList();
        if (!queryParams.Any()) return route;

        var sb = new StringBuilder(route);
        sb.Append("?");
        for (int i = 0; i < queryParams.Count; i++)
        {
            var p = queryParams[i];
            var value = asNotFound ? TestValues.AsNotFoundSegment(p.Type) : TestValues.AsRouteSegment(p.Type);
            sb.Append($"{p.Name}={value}");
            if (i < queryParams.Count - 1) sb.Append("&");
        }

        return sb.ToString();
    }

    private static string ResolveControllerName(string className)
    {
        const string suffix = "Controller";
        var name = className.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? className[..^suffix.Length] : className;
        return name.ToLowerInvariant();
    }

    private static string CombineSegments(string baseRoute, string? routeSegment)
    {
        if (string.IsNullOrWhiteSpace(routeSegment)) return baseRoute.Trim('/');

        return $"{baseRoute.Trim('/')}/{routeSegment.Trim('/')}";
    }

    private static string ReplaceRouteTokens(string route, List<ParameterDetail> parameters, bool asNotFound)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            route,
            @"\{([^}]+)\}",
            match =>
            {
                var token = match.Groups[1].Value.Split(':')[0].ToLowerInvariant();
                return ResolveTokenValue(token, parameters, asNotFound);
            });
    }

    private static string ResolveTokenValue(string token, List<ParameterDetail> parameters, bool asNotFound)
    {
        var match = parameters.FirstOrDefault(p =>
            p.IsFromRoute &&
            p.Name.Equals(token, StringComparison.OrdinalIgnoreCase));

        // Real type from analyzer — no guessing
        if (match is not null)
            return asNotFound ? TestValues.AsNotFoundSegment(match.Type) : TestValues.AsRouteSegment(match.Type);

        // True fallback — should rarely hit this
        return asNotFound ? TestValues.AsNotFoundSegment("int") : TestValues.AsRouteSegment("int");
    }
}
