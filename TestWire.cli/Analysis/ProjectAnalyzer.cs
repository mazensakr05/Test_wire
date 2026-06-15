using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace TestWire.cli.Analysis;

public class ProjectAnalyzer
{
    public static async Task<List<ControllerInfo>> AnalyzeAsync(string csprojPath)
    {
        if (!Microsoft.Build.Locator.MSBuildLocator.IsRegistered)
        {
            RegisterMSBuild();
        }

        var controllers = new List<ControllerInfo>();

        using var workspace = MSBuildWorkspace.Create();
        workspace.WorkspaceFailed += (s, e) => Console.WriteLine($"[MSBuild Warning] {e.Diagnostic.Message}");

        var project = await workspace.OpenProjectAsync(csprojPath);
        var compilation = await project.GetCompilationAsync();

        if (compilation == null)
        {
            var diagnostics = workspace.Diagnostics.Select(d => d.Message).ToList();
            throw new Exception($"Failed to compile project. Diagnostics: {string.Join(Environment.NewLine, diagnostics)}");
        }

        foreach (var document in project.Documents)
        {
            // Skip generated files in the obj/ folder — we only want user-written code
            if (document.FilePath?.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") == true)
                continue;

            var syntaxTree = await document.GetSyntaxTreeAsync();
            if (syntaxTree == null) continue;

            var root = await syntaxTree.GetRootAsync();

            // SemanticModel ties this file's syntax to the full compilation
            // This is what lets us resolve types across the entire project
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Find all classes ending in "Controller" in this file
            var classDeclarations = root
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(c => c.Identifier.Text.EndsWith("Controller"));

            foreach (var classDecl in classDeclarations)
            {
                // Cross from syntax → symbol — now we have full compiler knowledge
                var classSymbol = semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
                if (classSymbol == null) continue;

                // Fix: prevent duplicate processing when a controller is split
                // across multiple partial class files — only process the primary declaration
                var primaryTree = classSymbol.Locations
                    .FirstOrDefault(l => l.IsInSource)?.SourceTree;
                if (primaryTree != null && classDecl.SyntaxTree != primaryTree)
                    continue;

                var endpoints = new List<EndpointInfo>();
                
                var controllerInfo = new ControllerInfo(
                    classSymbol.Name,
                    classSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
                    GetAttributeArgument(classSymbol, "Route") ?? string.Empty,
                    endpoints,
                    GetConstructorDependencies(classSymbol)
                );

                // Iterate methods via symbol — not syntax — for full attribute resolution
                foreach (var member in classSymbol.GetMembers().OfType<IMethodSymbol>())
                {
                    // Only process methods decorated with an HTTP verb attribute
                    var verb = GetHttpVerb(member);
                    if (verb is null) continue;

                    // --- Three-Layer Return Type Resolution ---

                    // Layer 1: read directly from the method signature
                    // e.g. Task<ActionResult<ProductDto>> → "ProductDto"
                    var originalType = member.ReturnType;
                    var unwrapped = UnwrapReturnType(originalType);
                    var returnTypeKind = ReturnTypeKind.Unknown;

                    if (!string.IsNullOrEmpty(unwrapped))
                    {
                        // Only mark as ActionResultOfT if the signature actually
                        // contained ActionResult<T> — not a plain DTO return type
                        var isActionResultOfT = originalType is INamedTypeSymbol n
                            && (n.Name == "ActionResult"
                                || (n.Name == "Task"
                                    && n.TypeArguments.Length == 1
                                    && (n.TypeArguments[0] as INamedTypeSymbol)?.Name == "ActionResult"));

                        var isIActionResult = originalType is INamedTypeSymbol m
                            && (m.Name == "IActionResult"
                                || (m.Name == "Task"
                                    && m.TypeArguments.Length == 1
                                    && (m.TypeArguments[0] as INamedTypeSymbol)?.Name == "IActionResult"));

                        returnTypeKind = isActionResultOfT
                            ? ReturnTypeKind.ActionResultOfT
                            : isIActionResult
                                ? ReturnTypeKind.IActionResultWithInferredT
                                : ReturnTypeKind.PlainType;
                    }

                    // Compute once — reused for both Layer 2 inference and endpoint assignment
                    var producesResponses = GetProducesResponseDetails(member);

                    // Layer 2: check [ProducesResponseType(typeof(T), 200)] attribute
                    // catches IActionResult methods that declare their type via attribute
                    if (string.IsNullOrEmpty(unwrapped))
                    {
                        unwrapped = producesResponses
                            .FirstOrDefault(r => r.StatusCode == 200)?.TypeName
                            ?? string.Empty;

                        if (!string.IsNullOrEmpty(unwrapped))
                            returnTypeKind = ReturnTypeKind.IActionResultWithInferredT;
                    }

                    // Layer 3: crawl the method body for return Ok(x), Created(x) etc.
                    // asks the Semantic Model what type x is at each return statement
                    if (string.IsNullOrEmpty(unwrapped))
                    {
                        unwrapped = await TryInferReturnTypeFromBody(member, compilation)
                                    ?? string.Empty;

                        if (!string.IsNullOrEmpty(unwrapped))
                            returnTypeKind = ReturnTypeKind.IActionResultWithInferredT;
                    }
                    // Detect the actual status code from return statements (Ok→200, CreatedAtAction→201 etc.)
                    var detectedStatusCode = await TryInferStatusCodeFromBody(member, compilation);
                    // --- Build Endpoint ---

                    var parameters = new List<ParameterDetail>();

                    foreach (var param in member.Parameters)
                    {
                        var typeDisplay = GetTypeDisplayInfo(param.Type);
                        var dtoProperties = new List<PropertyDetail>();

                        // If the parameter is a user-defined class (DTO, Command, etc.)
                        // read its public properties so the generator can build request objects
                        if (param.Type is INamedTypeSymbol paramTypeSymbol
                            && IsComplexUserType(param.Type))
                        {
                            dtoProperties = ReadDtoProperties(paramTypeSymbol);
                        }

                        var paramDetail = new ParameterDetail(
                            param.Name,
                            typeDisplay.Type,
                            typeDisplay.FullyQualifiedType,
                            HasAttribute(param, "FromBody"),
                            HasAttribute(param, "FromRoute"),
                            HasAttribute(param, "FromQuery"),
                            HasAttribute(param, "FromHeader"),
                            dtoProperties
                        );

                        parameters.Add(paramDetail);
                    }

                    var endpointInfo = new EndpointInfo(
                        member.Name,
                        verb,
                        GetAttributeArgument(member, verb) ?? GetAttributeArgument(member, "Route") ?? string.Empty,
                        unwrapped,
                        returnTypeKind,
                        false, // HasAmbiguousReturnType
                        member.IsAsync,
                        (HasAttribute(member, "Authorize") || HasAttribute(classSymbol, "Authorize")) && !HasAttribute(member, "AllowAnonymous"),
                        HasAttribute(member, "AllowAnonymous"),
                        detectedStatusCode ?? 200,
                        parameters,
                        producesResponses
                    );

                    endpoints.Add(endpointInfo);
                }

                controllers.Add(controllerInfo);
            }
        }

        return controllers;
    }

    public static List<PropertyDetail> ReadDtoProperties(INamedTypeSymbol typeSymbol)
    {
        var list = new List<PropertyDetail>();

        foreach (var member in typeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            // Fix: skip indexers and get-only properties — the generator writes object
            // initializers, so any property without a public setter produces uncompilable tests
            if (member.IsIndexer) continue;

            var setMethod = member.SetMethod;
            if (member.DeclaredAccessibility == Accessibility.Public
                && !member.IsStatic
                && setMethod is not null
                && setMethod.DeclaredAccessibility == Accessibility.Public)
            {
                var typeDisplay = GetTypeDisplayInfo(member.Type);
                list.Add(new PropertyDetail(
                    member.Name,
                    typeDisplay.Type,
                    typeDisplay.FullyQualifiedType
                ));
            }
        }

        return list;
    }

    private static List<ConstructorDependency> GetConstructorDependencies(INamedTypeSymbol classSymbol)
    {
        var constructors = classSymbol.Constructors;
        if (constructors.Length == 0) return new List<ConstructorDependency>();

        // Pick the constructor with the most parameters — that's the DI constructor
        var primary = constructors.OrderByDescending(c => c.Parameters.Length).First();
        var dependencies = new List<ConstructorDependency>();

        foreach (var parameter in primary.Parameters)
        {
            var typeDisplay = GetTypeDisplayInfo(parameter.Type);
            dependencies.Add(new ConstructorDependency(
                parameter.Name,
                typeDisplay.FullyQualifiedType
            ));
        }

        return dependencies;
    }

    /// <summary>
    /// Registers the MSBuild assemblies using a robust multi-strategy waterfall.
    ///
    /// Strategy 1 — QueryVisualStudioInstances (SDK-first):
    ///   Uses MSBuildLocator's built-in discovery. On most developer machines this finds
    ///   the installed .NET SDK automatically. We prefer DotNetSdk over VS instances.
    ///
    /// Strategy 2 — Subprocess dotnet CLI fallback:
    ///   If the locator finds nothing (common on CI runners, minimal Docker images,
    ///   or machines where DOTNET_ROOT is not set as a system env-var), we run
    ///   `dotnet --list-sdks` ourselves to discover the SDK location and call
    ///   RegisterMSBuildPath() directly on the resolved MSBuild folder.
    ///
    /// This is the same pattern used by dotnet-format and other Roslyn-based tools
    /// to work portably across every machine without requiring DOTNET_ROOT or VS.
    /// </summary>
    private static void RegisterMSBuild()
    {
        // ── Strategy 1: use the built-in locator ──────────────────────────────
        var instances = Microsoft.Build.Locator.MSBuildLocator.QueryVisualStudioInstances().ToList();

        // Prefer a .NET SDK entry (not VS); fall back to any found instance
        var best = instances.FirstOrDefault(i =>
                       i.DiscoveryType == Microsoft.Build.Locator.DiscoveryType.DotNetSdk)
                   ?? instances.FirstOrDefault();

        if (best != null)
        {
            Console.WriteLine($"[TestWire] Using MSBuild from: {best.MSBuildPath}");
            Microsoft.Build.Locator.MSBuildLocator.RegisterInstance(best);
            return;
        }

        // ── Strategy 2: discover SDK via `dotnet --list-sdks` subprocess ──────
        // This covers SDK-only machines, CI runners, and any machine where the
        // built-in discovery cannot enumerate instances from the environment.
        Console.WriteLine("[TestWire] MSBuildLocator found no instances; falling back to dotnet CLI discovery…");

        var sdkPath = TryResolveSdkPathViaCli();
        if (sdkPath != null)
        {
            var msbuildPath = Path.Combine(sdkPath, "MSBuild.dll");
            if (File.Exists(msbuildPath))
            {
                Console.WriteLine($"[TestWire] Registering MSBuild at: {sdkPath}");
                Microsoft.Build.Locator.MSBuildLocator.RegisterMSBuildPath(sdkPath);
                return;
            }
        }

        // ── Last resort: let RegisterDefaults throw a clear error ──────────────
        // If we reach here the .NET SDK is genuinely not installed or broken.
        try
        {
            Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults();
        }
        catch (Exception inner)
        {
            throw new InvalidOperationException(
                "[TestWire] Could not locate MSBuild. " +
                "Make sure the .NET SDK is installed (https://dot.net) and that " +
                "`dotnet --version` works in your terminal. " +
                $"Inner error: {inner.Message}", inner);
        }
    }

    /// <summary>
    /// Runs `dotnet --list-sdks` and returns the directory of the highest-version SDK,
    /// or null if the dotnet CLI is not reachable or returns nothing useful.
    /// </summary>
    private static string? TryResolveSdkPathViaCli()
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet", "--list-sdks")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };

            using var proc = Process.Start(psi);
            if (proc == null) return null;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5_000); // never hang the CLI for more than 5 s

            // Output format from `dotnet --list-sdks`:
            //   9.0.306 [C:\Program Files\dotnet\sdk]
            //   10.0.100 [C:\Program Files\dotnet\sdk]
            //
            // The bracketed part is the BASE directory for all SDKs.
            // The actual version folder is: [base]\[version]  e.g.
            //   C:\Program Files\dotnet\sdk\9.0.306\MSBuild.dll
            var sdkVersionDirs = output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line =>
                {
                    line = line.Trim();
                    var spaceIdx = line.IndexOf(' ');
                    if (spaceIdx < 0) return null;

                    var version = line.Substring(0, spaceIdx).Trim();

                    var start = line.IndexOf('[');
                    var end   = line.IndexOf(']');
                    if (start < 0 || end <= start) return null;

                    var baseDir = line.Substring(start + 1, end - start - 1).Trim();

                    // Combine base directory with the version subfolder
                    return Path.Combine(baseDir, version);
                })
                .Where(d => d != null && Directory.Exists(d))
                .ToList();

            // Pick the highest semantic version SDK that:
            // 1. actually has MSBuild.dll
            // 2. has the same major version as the currently running .NET runtime
            //    (e.g. if we run under net8, prefer an 8.x SDK not a 10.x one —
            //     loading a .NET 10 MSBuild from a net8 process causes assembly
            //     binding failures for System.Runtime v10).
            var runtimeMajor = Environment.Version.Major;

            var compatible = sdkVersionDirs
                .Where(d => File.Exists(Path.Combine(d!, "MSBuild.dll")))
                .Select(d =>
                {
                    var versionPart = Path.GetFileName(d!);
                    Version.TryParse(versionPart, out var v);
                    return (Path: d, Version: v ?? new Version(0, 0));
                })
                .OrderByDescending(x => x.Version)
                .ToList();

            // First try: same major version as the running runtime
            var best = compatible.FirstOrDefault(x => x.Version.Major == runtimeMajor);

            // Fallback: any lower major version (still safer than a newer one)
            if (best.Path == null)
                best = compatible.FirstOrDefault(x => x.Version.Major < runtimeMajor);

            // Last fallback: whatever we have (better than nothing)
            if (best.Path == null)
                best = compatible.FirstOrDefault();

            return best.Path;
        }
        catch
        {
            // dotnet CLI not on PATH — nothing we can do here
            return null;
        }
    }

    private static string UnwrapReturnType(ITypeSymbol typeSymbol)
    {
        // These are the wrapper types we peel off to get to the real return type
        string[] wrappers = ["Task", "ActionResult", "IActionResult"];

        if (typeSymbol is not INamedTypeSymbol namedType)
            return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);

        if (wrappers.Contains(namedType.Name))
        {
            // Has a generic argument — recurse to unwrap the next layer
            // e.g. Task<ActionResult<ProductDto>> → ActionResult<ProductDto> → ProductDto
            return namedType.TypeArguments.Length > 0
                ? UnwrapReturnType(namedType.TypeArguments[0])
                : string.Empty; // plain Task / IActionResult — no type info
        }

        return namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
    }

    private static (string Type, string FullyQualifiedType) GetTypeDisplayInfo(ITypeSymbol typeSymbol)
    {
        var type = typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        var fullyQualifiedType = typeSymbol.SpecialType != SpecialType.None
            ? type
            : typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Replace("global::", string.Empty);

        return (type, fullyQualifiedType);
    }

    private static bool HasAttribute(ISymbol symbol, string attributeName)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            var name = attr.AttributeClass?.Name;
            if (name == null) continue;

            var cleanName = name.EndsWith("Attribute")
                ? name.Substring(0, name.Length - 9)
                : name;

            if (cleanName == attributeName)
                return true;
        }

        return false;
    }

    private static string? GetAttributeArgument(ISymbol symbol, string attributeName)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            var name = attr.AttributeClass?.Name;
            if (name == null) continue;

            var cleanName = name.EndsWith("Attribute")
                ? name.Substring(0, name.Length - 9)
                : name;

            if (cleanName == attributeName && attr.ConstructorArguments.Length > 0)
                return attr.ConstructorArguments[0].Value?.ToString();
        }

        return null;
    }

    private static string? GetHttpVerb(IMethodSymbol methodSymbol)
    {
        string[] verbs = ["HttpGet", "HttpPost", "HttpPut", "HttpDelete", "HttpPatch"];

        foreach (var verb in verbs)
        {
            if (HasAttribute(methodSymbol, verb))
                return verb;
        }

        return null;
    }

    private static bool IsComplexUserType(ITypeSymbol typeSymbol)
    {
        // Primitives (int, string, bool etc.) — stop
        if (typeSymbol.SpecialType != SpecialType.None)
            return false;

        // System/Microsoft framework types — stop
        var ns = typeSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (ns.StartsWith("System") || ns.StartsWith("Microsoft"))
            return false;

        // Only user-defined classes and structs qualify
        return typeSymbol.TypeKind == TypeKind.Class ||
               typeSymbol.TypeKind == TypeKind.Struct;
    }

    private static List<ProducesResponseDetail> GetProducesResponseDetails(IMethodSymbol methodSymbol)
    {
        var result = new List<ProducesResponseDetail>();

        foreach (var attr in methodSymbol.GetAttributes())
        {
            var name = attr.AttributeClass?.Name;
            if (name == null) continue;

            var cleanName = name.EndsWith("Attribute")
                ? name.Substring(0, name.Length - 9)
                : name;

            if (cleanName != "ProducesResponseType") continue;
            if (attr.ConstructorArguments.Length == 0) continue;

            // Case 1: [ProducesResponseType(typeof(ProductDto), 200)]
            if (attr.ConstructorArguments[0].Kind == TypedConstantKind.Type)
            {
                var typeSymbol = attr.ConstructorArguments[0].Value as ITypeSymbol;
                var typeName = typeSymbol?
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)?.Replace("global::", string.Empty);

                var statusCode = attr.ConstructorArguments.Length > 1
                    ? (int)(attr.ConstructorArguments[1].Value ?? 200)
                    : 200;
                    
                result.Add(new ProducesResponseDetail(statusCode, typeName));
            }
            // Case 2: [ProducesResponseType(404)]
            else if (attr.ConstructorArguments[0].Kind == TypedConstantKind.Primitive)
            {
                var statusCode = (int)(attr.ConstructorArguments[0].Value ?? 200);
                result.Add(new ProducesResponseDetail(statusCode, null));
            }
        }

        return result;
    }

    private static async Task<string?> TryInferReturnTypeFromBody(
        IMethodSymbol methodSymbol,
        Compilation compilation)
    {
        var syntaxRef = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null) return null;

        var methodSyntax = await syntaxRef.GetSyntaxAsync() as MethodDeclarationSyntax;
        if (methodSyntax == null) return null;

        var correctModel = compilation.GetSemanticModel(methodSyntax.SyntaxTree);

        var resultMethods = new HashSet<string>
            { "Ok", "Created", "CreatedAtAction", "CreatedAtRoute", "Accepted", "AcceptedAtAction" };

        IEnumerable<InvocationExpressionSyntax> invocations;

        // Block body — only invocations directly inside a return statement
        if (methodSyntax.Body != null)
        {
            invocations = methodSyntax.Body
                .DescendantNodes()
                .OfType<ReturnStatementSyntax>()
                .Select(r => r.Expression)
                .OfType<InvocationExpressionSyntax>();
        }
        // Expression body — the single expression IS the return
        else if (methodSyntax.ExpressionBody != null)
        {
            invocations = methodSyntax.ExpressionBody
                .DescendantNodesAndSelf()
                .OfType<InvocationExpressionSyntax>();
        }
        else return null;

        foreach (var invocation in invocations)
        {
            // Extract method name — handles both Ok(x) and this.Ok(x)
            var methodName = invocation.Expression is MemberAccessExpressionSyntax memberAccess
                ? memberAccess.Name.Identifier.Text
                : (invocation.Expression as IdentifierNameSyntax)?.Identifier.Text;

            if (methodName is null || !resultMethods.Contains(methodName)) continue;

            var arg = invocation.ArgumentList.Arguments.FirstOrDefault();
            if (arg == null) continue;

            // Ask the compiler what type this argument expression resolves to
            var typeInfo = correctModel.GetTypeInfo(arg.Expression);
            if (typeInfo.Type is null) continue;

            return typeInfo.Type
                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
        }

        return null;
    }
    private static async Task<int?> TryInferStatusCodeFromBody(IMethodSymbol methodSymbol, Compilation compilation)
    {

        var syntaxRef = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null) return null;

        var methodSyntax = await syntaxRef.GetSyntaxAsync() as MethodDeclarationSyntax;
        if (methodSyntax == null) return null;

        var statusCodeMap = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Ok"] = 200,
            ["Created"] = 201,
            ["CreatedAtAction"] = 201,
            ["CreatedAtRoute"] = 201,
            ["Accepted"] = 202,
            ["AcceptedAtAction"] = 202,
            ["NoContent"] = 204,
            ["BadRequest"] = 400,
            ["NotFound"] = 404,
            ["Conflict"] = 409,
        };
        IEnumerable<InvocationExpressionSyntax> invocations;
        if (methodSyntax.Body != null)
        {
            invocations = methodSyntax.Body
                .DescendantNodes()
                .OfType<ReturnStatementSyntax>()
                .Select(r => r.Expression)
                .OfType<InvocationExpressionSyntax>();
        }
        else if (methodSyntax.ExpressionBody != null)
        {
            invocations = methodSyntax.ExpressionBody
                .DescendantNodesAndSelf()
                .OfType<InvocationExpressionSyntax>();
        }
        else return null;

        int? firstFound = null;
        foreach (var invocation in invocations)
        {
            var methodName = invocation.Expression is MemberAccessExpressionSyntax memberAccess
                ? memberAccess.Name.Identifier.Text
                : (invocation.Expression as IdentifierNameSyntax)?.Identifier.Text;
            
            if (methodName != null && statusCodeMap.TryGetValue(methodName, out var code))
            {
                // Bugfix: Prioritise 2xx success codes for the "happy path" over error codes.
                // If a controller does: `if (x == null) return NotFound(); return Ok(x);`
                // we MUST pick Ok() to generate the passing test.
                if (code is >= 200 and < 300)
                    return code;
                    
                firstFound ??= code;
            }
        }
        
    return firstFound;
}
}