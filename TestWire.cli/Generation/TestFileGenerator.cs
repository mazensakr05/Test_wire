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
        // AFTER — emit the root namespace first (always needed for Program reference)
        sb.AppendLine($"using {context.ProjectNamespace};");

        // Then emit every namespace Roslyn actually told us about
        foreach (var ns in CollectRequiredNamespaces(controller, context.ProjectNamespace))
            sb.AppendLine($"using {ns};"); sb.AppendLine();

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
        var postEndpoint = controller.Endpoints.FirstOrDefault(e => e.HttpVerb == "HttpPost");
        var getByIdEndpoint = controller.Endpoints.FirstOrDefault(e =>
            e.HttpVerb == "HttpGet" &&
            e.Parameters.Count(p => p.IsFromRoute) == 1 &&
            !string.IsNullOrWhiteSpace(e.ReturnType) &&
            e.ReturnType == postEndpoint?.ReturnType);

        // null check + guard against empty ReturnType (avoids ReadFromJsonAsync<>() )
        if (postEndpoint is not null
            && getByIdEndpoint is not null
            && !string.IsNullOrWhiteSpace(postEndpoint.ReturnType))
        {
            var idPropertyName = ResolveIdPropertyName(postEndpoint.ReturnTypeProperties);

            // TODO: If idPropertyName is null, the CLI could later ask the user interactively:
            // "What is the identifier property name for {postEndpoint.ReturnType}?"
            // and pass that override into the generator.
            if (idPropertyName is not null)
            {
                sb.Append(BuildCreateThenReadTest(postEndpoint, getByIdEndpoint, controller, idPropertyName));
            }
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

    /// <summary>
    /// Walks every endpoint in the controller and collects the namespaces
    /// of every user-defined type that the generated test file will reference.
    ///
    /// Why do we need this?
    /// The generated test constructs DTOs, reads response bodies, and asserts
    /// on return types. Every one of those types must have a corresponding
    /// "using" statement or the file won't compile.
    ///
    /// How does it work?
    /// ProjectAnalyzer already asked Roslyn for the fully-qualified name of
    /// every type (e.g. "MyApi.Features.Products.CreateProductDto").
    /// We just strip the last segment to get the namespace:
    ///   "MyApi.Features.Products.CreateProductDto" → "MyApi.Features.Products"
    /// </summary>
    private static IEnumerable<string> CollectRequiredNamespaces(
        ControllerInfo controller,
        string projectNamespace)
    {
        var namespaces = new HashSet<string>(StringComparer.Ordinal);

        foreach (var endpoint in controller.Endpoints)
        {
            // Source 1: the return type  e.g. "MyApi.Domain.Product"
            TryAddNamespace(endpoint.ReturnType, namespaces);

            foreach (var param in endpoint.Parameters)
            {
                // Source 2: the parameter type itself  e.g. "MyApi.Features.Products.CreateProductDto"
                TryAddNamespace(param.FullyQualifiedType, namespaces);

                // Source 3: each property inside the DTO
                // e.g. a property of type "MyApi.Common.Money"
                foreach (var prop in param.DtoProperties)
                    TryAddNamespace(prop.FullyQualifiedType, namespaces);
            }
        }

        return namespaces
            .Where(ns =>
                // Skip System.* and Microsoft.* — already covered by standard usings
                !ns.StartsWith("System", StringComparison.Ordinal) &&
                !ns.StartsWith("Microsoft", StringComparison.Ordinal) &&
                // Skip the root project namespace — already emitted separately
                ns != projectNamespace &&
                // Skip anything that has no namespace (primitives, value types)
                !string.IsNullOrWhiteSpace(ns))
            .OrderBy(ns => ns); // deterministic output — same order every time
    }

    /// <summary>
    /// Extracts the namespace from a fully-qualified type name and adds it
    /// to the set. If the type has no dot (e.g. "int", "string") it has
    /// no namespace — we skip it safely.
    /// </summary>
    private static void TryAddNamespace(string fullyQualifiedType, HashSet<string> namespaces)
    {
        if (string.IsNullOrWhiteSpace(fullyQualifiedType)) return;

        // Strip generic arguments first — e.g. "List<MyApi.Models.Product>"
        // We only need the outermost namespace here; generic type args
        // are handled when their own endpoints/parameters are processed
        var clean = fullyQualifiedType.Contains('<')
            ? fullyQualifiedType.Substring(0, fullyQualifiedType.IndexOf('<'))
            : fullyQualifiedType;

        var lastDot = clean.LastIndexOf('.');
        if (lastDot <= 0) return; // no dot = primitive or single-word type, skip

        var ns = clean.Substring(0, lastDot);
        namespaces.Add(ns);
    }
    private static readonly string[] _identifierCandidates = new[]
    {
        "Id", "Sku", "Guid", "Uid", "Key", "Code", "ReferenceId", "ExternalId", "Slug"
    };

    private static string? ResolveIdPropertyName(List<PropertyDetail>? returnTypeProperties)
    {
        if (returnTypeProperties is null || returnTypeProperties.Count == 0)
            return null;

        foreach (var candidate in _identifierCandidates)
        {
            var exactMatch = returnTypeProperties.FirstOrDefault(p =>
                string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase));
            if (exactMatch is not null) return exactMatch.Name;
        }

        var suffixMatch = returnTypeProperties.FirstOrDefault(p =>
            p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase));

        return null;
    }

    private static string BuildCreateThenReadTest(
    EndpointInfo postEndpoint,
    EndpointInfo getByIdEndpoint,
    ControllerInfo controller,
    string idPropertyName)
    {
        var sb = new StringBuilder();
        var client = (postEndpoint.HasAuthorize || getByIdEndpoint.HasAuthorize) ? "_authClient" : "_client";
        var postUrl = RouteBuilder.Build(
            controller.BaseRoute, controller.ClassName, postEndpoint.Route, postEndpoint.Parameters);

        var bodyParam = postEndpoint.Parameters.FirstOrDefault(p => p.IsFromBody);

        sb.AppendLine("    [Fact]");
        sb.AppendLine("    public async Task CreateThenGet_ReturnsCreatedResource()");
        sb.AppendLine("    {");

        if (bodyParam is not null)
        {
            sb.AppendLine($"        var request = new {bodyParam.FullyQualifiedType}");
            sb.AppendLine("        {");
            foreach (var prop in bodyParam.DtoProperties)
            {
                sb.AppendLine($"            {prop.Name} = {TestValues.AsExpression(prop.Type)},");
            }
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine($"        var createResponse = await {client}.PostAsJsonAsync(\"{postUrl}\", request);");
        }
        else
        {
            sb.AppendLine($"        var createResponse = await {client}.PostAsJsonAsync(\"{postUrl}\", new {{ }});");
        }

        sb.AppendLine("        createResponse.EnsureSuccessStatusCode();");
        sb.AppendLine($"        var created = await createResponse.Content.ReadFromJsonAsync<{postEndpoint.ReturnType}>();");
        sb.AppendLine();

        var baseGetRoute = controller.BaseRoute.Replace("[controller]",
            controller.ClassName.Replace("Controller", "").ToLowerInvariant());

        sb.AppendLine($"        var getResponse = await {client}.GetAsync($\"{baseGetRoute}/{{created.{idPropertyName}}}\");");
        sb.AppendLine("        getResponse.EnsureSuccessStatusCode();");
        sb.AppendLine($"        var retrieved = await getResponse.Content.ReadFromJsonAsync<{getByIdEndpoint.ReturnType}>();");
        sb.AppendLine();
        sb.AppendLine("        Assert.NotNull(retrieved);");
        sb.AppendLine("    }");
        sb.AppendLine();

        return sb.ToString();
    }
}


