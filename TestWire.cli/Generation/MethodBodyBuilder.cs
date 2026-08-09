using System.Text;
using System.Text.RegularExpressions;
using TestWire.cli.Analysis;

namespace TestWire.cli.Generation;

public static class MethodBodyBuilder
{
    public static string Build(EndpointInfo endpoint, string url)
    {
        var sb = new StringBuilder();
        var testName = BuildTestName(endpoint);
        var bodyParam = endpoint.Parameters.FirstOrDefault(p => p.IsFromBody);
        var client = endpoint.HasAuthorize ? "_authClient" : "_client";

        sb.AppendLine("    [Fact]");
        sb.AppendLine($"    public async Task {testName}()");
        sb.AppendLine("    {");

        AppendRequestBody(sb, bodyParam);

        var httpCall = BuildHttpCall(endpoint.HttpVerb, url, client, bodyParam is not null);
        sb.AppendLine($"        var response = {httpCall}");

        var expectedStatusCode = TestValues.ToStatusCodeExpression(endpoint.ExpectedStatusCode);
        sb.AppendLine($"        Assert.Equal({expectedStatusCode}, response.StatusCode);");

        // Bugfix: Do not attempt to deserialize response bodies on 204 or 4xx/5xx responses
        if (!string.IsNullOrEmpty(endpoint.ReturnType) && TestValues.HasResponseBody(endpoint.ExpectedStatusCode))
        {
            sb.AppendLine($"        var result = await response.Content.ReadFromJsonAsync<{endpoint.ReturnType}>();");
            sb.AppendLine("        Assert.NotNull(result);");
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        return sb.ToString();
    }

    public static string BuildNotFoundTest(EndpointInfo endpoint, string baseRoute, string className)
    {
        var sb = new StringBuilder();
        // Delegate to RouteBuilder which uses the unified TestValues.AsNotFoundSegment
        var notFoundUrl = RouteBuilder.BuildNotFound(baseRoute, className, endpoint.Route, endpoint.Parameters);
        var bodyParam = endpoint.Parameters.FirstOrDefault(p => p.IsFromBody);
        var client = endpoint.HasAuthorize ? "_authClient" : "_client";

        sb.AppendLine("    [Fact]");
        sb.AppendLine($"    public async Task {endpoint.MethodName}_Returns404_WhenNotFound()");
        sb.AppendLine("    {");

        AppendRequestBody(sb, bodyParam);

        var httpCall = BuildHttpCall(endpoint.HttpVerb, notFoundUrl, client, bodyParam is not null);
        sb.AppendLine($"        var response = {httpCall}");
        sb.AppendLine("        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);");
        sb.AppendLine("    }");
        sb.AppendLine();

        return sb.ToString();
    }

    private static void AppendRequestBody(StringBuilder sb, ParameterDetail? bodyParam)
    {
        if (bodyParam is null) return;

        sb.AppendLine($"        var request = new {bodyParam.FullyQualifiedType}");
        sb.AppendLine("        {");
        foreach (var prop in bodyParam.DtoProperties)
        {
            var valueExpression = BuildValidValueExpression(prop);
            sb.AppendLine($"            {prop.Name} = {valueExpression},");
        }
        sb.AppendLine("        };");
        sb.AppendLine();
    }

    private static string BuildHttpCall(string httpVerb, string url, string client, bool hasBody)
    {
        return httpVerb switch
        {
            "HttpGet"    => $"await {client}.GetAsync(\"{url}\");",
            "HttpDelete" => $"await {client}.DeleteAsync(\"{url}\");",
            "HttpPost"   => hasBody
                                ? $"await {client}.PostAsJsonAsync(\"{url}\", request);"
                                : $"await {client}.PostAsJsonAsync(\"{url}\", new {{ }});",
            "HttpPut"    => hasBody
                                ? $"await {client}.PutAsJsonAsync(\"{url}\", request);"
                                : $"await {client}.PutAsJsonAsync(\"{url}\", new {{ }});",
            "HttpPatch"  => hasBody
                                ? $"await {client}.PatchAsJsonAsync(\"{url}\", request);"
                                : $"await {client}.PatchAsJsonAsync(\"{url}\", new {{ }});",
            _            => $"await {client}.GetAsync(\"{url}\");"
        };
    }

    private static string BuildTestName(EndpointInfo endpoint)
    {
        if (string.IsNullOrEmpty(endpoint.ReturnType))
            return $"{endpoint.MethodName}_Returns{endpoint.ExpectedStatusCode}";

        var sanitizedType = Regex.Replace(endpoint.ReturnType, @"[^a-zA-Z0-9]", "_");
        sanitizedType = Regex.Replace(sanitizedType, @"_+", "_").Trim('_');

        return $"{endpoint.MethodName}_Returns{endpoint.ExpectedStatusCode}_With{CapitalizeFirst(sanitizedType)}";
    }

    private static string CapitalizeFirst(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    private static string BuildValidValueExpression(PropertyDetail prop)
    {
        var type = prop.Type;

        var required = prop.ValidationAttributes.Any(a => a.Name == "Required");
        var range = prop.ValidationAttributes.FirstOrDefault(a => a.Name == "Range");
        var maxLength = prop.ValidationAttributes.FirstOrDefault(a => a.Name == "MaxLength");

        if (range is not null && (type == "int" || type == "decimal" || type == "double"))
        {
            if (range.Arguments.Count >= 2 &&
                decimal.TryParse(range.Arguments[0], out var min) &&
                decimal.TryParse(range.Arguments[1], out var max))
            {
                var value = (min + max) / 2;
                return type switch
                {
                    "int" => ((int)value).ToString(),
                    "double" => value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "decimal" => value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "m",
                    _ => TestValues.AsExpression(type)
                };
            }
        }

        if (type == "string")
        {
            var baseValue = "\"test\"";

            if (maxLength is not null &&
                maxLength.Arguments.Count >= 1 &&
                int.TryParse(maxLength.Arguments[0], out var maxLen))
            {
                var length = Math.Min(maxLen, 10);
                var content = new string('a', Math.Max(length, 1));
                return $"\"{content}\"";
            }

            if (required)
                return baseValue;

            return baseValue;
        }

        return TestValues.AsExpression(type);
    }
}





