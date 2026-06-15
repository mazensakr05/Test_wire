using System.Net.Http.Headers;
using System.Text;
using TestWire.cli.Analysis;

namespace TestWire.cli.Generation;

public static class TestFileGenerator
{
    public static string Generate(ControllerInfo controller, GenerationContext context)
    {
        var sb = new StringBuilder();

        // Usings for the generated test file
        if (context.Framework == TestFramework.NUnit)
            sb.AppendLine("using NUnit.Framework;");
        else
            sb.AppendLine("using Xunit;");

        sb.AppendLine("using System.Net;");
        sb.AppendLine("using System.Net.Http.Json;");
        sb.AppendLine("using System.Net.Http.Headers;");
        sb.AppendLine("using Microsoft.AspNetCore.Mvc.Testing;");
        sb.AppendLine($"using {context.ProjectNamespace};");
        sb.AppendLine();

        // Namespace of the generated test file
        sb.AppendLine($"namespace {context.ProjectNamespace}.Tests;");
        sb.AppendLine();

        // Class declaration — uses CustomWebApplicationFactory so fake auth is active
        sb.AppendLine($"public class {controller.ClassName}Tests : IClassFixture<CustomWebApplicationFactory>");
        sb.AppendLine("{");

        // Two clients:
        // _client      → anonymous, no auth header → used for 401 tests
        // _authClient  → carries fake "Test" scheme header → used for happy path tests
        sb.AppendLine("    private readonly HttpClient _client;");
        sb.AppendLine("    private readonly HttpClient _authClient;");
        sb.AppendLine();

        sb.AppendLine($"    public {controller.ClassName}Tests(CustomWebApplicationFactory factory)");
        sb.AppendLine("    {");
        sb.AppendLine("        _client = factory.CreateClient();");
        sb.AppendLine("        _authClient = factory.CreateClient();");
        sb.AppendLine("        _authClient.DefaultRequestHeaders.Authorization =");
        sb.AppendLine("            new AuthenticationHeaderValue(\"Test\", \"testwire\");");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate one or more test methods per endpoint
        foreach (var endpoint in controller.Endpoints)
        {
            var url = RouteBuilder.Build(
                controller.BaseRoute,
                controller.ClassName,
                endpoint.Route,
                endpoint.Parameters);

            // Happy path test — uses _authClient if [Authorize] is present
            sb.Append(MethodBodyBuilder.Build(endpoint, url));

            // 401 test — always uses _client (intentionally no auth)
            if (endpoint.HasAuthorize)
                sb.Append(BuildUnauthorizedTest(endpoint, url));

            // 404 test — uses _authClient if [Authorize] is present
            if (ShouldGenerate404Test(endpoint))
                sb.Append(MethodBodyBuilder.BuildNotFoundTest(
                    endpoint,
                    controller.BaseRoute,
                    controller.ClassName));
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    // Intentionally uses _client — no auth header
    // Purpose: verify the endpoint returns 401 when unauthenticated
    private static string BuildUnauthorizedTest(EndpointInfo endpoint, string url)
    {
        var sb = new StringBuilder();

        var verb = endpoint.HttpVerb switch
        {
            "HttpGet" => $"await _client.GetAsync(\"{url}\");",
            "HttpDelete" => $"await _client.DeleteAsync(\"{url}\");",
            "HttpPost" => $"await _client.PostAsJsonAsync(\"{url}\", new {{ }});",
            "HttpPut" => $"await _client.PutAsJsonAsync(\"{url}\", new {{ }});",
            "HttpPatch" => $"await _client.PatchAsJsonAsync(\"{url}\", new {{ }});",
            _ => $"await _client.GetAsync(\"{url}\");"
        };

        sb.AppendLine("    [Fact]");
        sb.AppendLine($"    public async Task {endpoint.MethodName}_Returns401_WhenUnauthenticated()");
        sb.AppendLine("    {");
        sb.AppendLine($"        var response = {verb}");
        sb.AppendLine("        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);");
        sb.AppendLine("    }");
        sb.AppendLine();

        return sb.ToString();
    }

    private static bool ShouldGenerate404Test(EndpointInfo endpoint)
    {
        var supportedVerbs = new[] { "HttpGet", "HttpPut", "HttpDelete", "HttpPatch" };

        return supportedVerbs.Contains(endpoint.HttpVerb)
            && endpoint.Parameters.Any(p => p.IsFromRoute);
    }
}