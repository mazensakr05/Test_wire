using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TestWire.cli.Analysis;
using Xunit;

namespace TestWire.Tests.Analysis;

public class ProjectAnalyzerTests
{
    private static Compilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute).Assembly.Location)
        };

        return CSharpCompilation.Create("TestCompilation",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static IMethodSymbol GetMethodSymbol(string source, string methodName)
    {
        var compilation = CreateCompilation(source);
        var syntaxTree = compilation.SyntaxTrees.First();
        var semanticModel = compilation.GetSemanticModel(syntaxTree);

        var methodDeclaration = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == methodName);

        return semanticModel.GetDeclaredSymbol(methodDeclaration)!;
    }

    [Theory]
    [InlineData("HttpGet", "HttpGet")]
    [InlineData("HttpPost", "HttpPost")]
    [InlineData("HttpPut", "HttpPut")]
    [InlineData("HttpDelete", "HttpDelete")]
    [InlineData("HttpPatch", "HttpPatch")]
    public void GetHttpVerb_IdentifiesCorrectVerb(string attributeName, string expectedVerb)
    {
        var source = $@"
        using System;
        public class {attributeName}Attribute : Attribute {{ }}

        public class TestController
        {{
            [{attributeName}]
            public void TestMethod() {{ }}
        }}";

        var methodSymbol = GetMethodSymbol(source, "TestMethod");

        var verb = ProjectAnalyzer.GetHttpVerb(methodSymbol);

        verb.Should().Be(expectedVerb);
    }

    [Fact]
    public void GetHttpVerb_ReturnsNullWhenNoVerbAttribute()
    {
        var source = @"
        public class TestController
        {
            public void TestMethod() { }
        }";

        var methodSymbol = GetMethodSymbol(source, "TestMethod");

        var verb = ProjectAnalyzer.GetHttpVerb(methodSymbol);

        verb.Should().BeNull();
    }

    [Fact]
    public void HasAttribute_ReturnsTrueWhenAttributeIsPresent()
    {
        var source = @"
        using System;
        public class AuthorizeAttribute : Attribute { }

        public class TestController
        {
            [Authorize]
            public void ProtectedMethod() { }
        }";

        var methodSymbol = GetMethodSymbol(source, "ProtectedMethod");

        var hasAttr = ProjectAnalyzer.HasAttribute(methodSymbol, "Authorize");

        hasAttr.Should().BeTrue();
    }

    [Fact]
    public void IsImplicitRouteParam_IsTrue_WhenParameterNameMatchesRouteTemplate_WithNoAttribute()
    {
        // Most common real-world pattern — no [FromRoute], but "id" matches "{id}"
        var routeTemplate = "{id}";
        var parameterName = "id";

        var result = ProjectAnalyzer.IsImplicitRouteParam(routeTemplate, parameterName);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsImplicitRouteParam_IsTrue_WhenRouteHasConstraint_LikeIdColonInt()
    {
        // "{id:int}" — constraint must be stripped before matching
        var routeTemplate = "{id:int}";
        var parameterName = "id";

        var result = ProjectAnalyzer.IsImplicitRouteParam(routeTemplate, parameterName);

        result.Should().BeTrue();
    }
    [Fact]
    public void ReadDtoProperties_CanReadReturnTypeProperties_ForResponseDto()
    {
        // Simulates a real response DTO — same shape as CategoryDto
        var source = @"
    public class CategoryDto
    {
        public int CategoryId { get; set; }
        public string Name { get; set; }
    }";

        var compilation = CreateCompilation(source);
        var syntaxTree = compilation.SyntaxTrees.First();
        var semanticModel = compilation.GetSemanticModel(syntaxTree);

        var classDeclaration = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "CategoryDto");

        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
        classSymbol.Should().NotBeNull();

        // Act — this is the SAME method used for request DTOs.
        // We're proving it also works correctly when applied to a RETURN type.
        var properties = ProjectAnalyzer.ReadDtoProperties(classSymbol!);

        // Assert — the analyzer should correctly read "CategoryId", not assume "Id"
        properties.Should().Contain(p => p.Name == "CategoryId");
        properties.Should().NotContain(p => p.Name == "Id");
    }
    [Fact]
    public void IsImplicitRouteParam_IsTrue_WhenRouteHasGuidConstraint()
    {
        // "{id:guid}" — same stripping logic must work for other constraint types
        var routeTemplate = "{id:guid}";
        var parameterName = "id";

        var result = ProjectAnalyzer.IsImplicitRouteParam(routeTemplate, parameterName);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsImplicitRouteParam_IsFalse_WhenParameterNameDoesNotMatchRouteTemplate()
    {
        // "search" is a query param — not present in the route at all
        var routeTemplate = "{id}";
        var parameterName = "search";

        var result = ProjectAnalyzer.IsImplicitRouteParam(routeTemplate, parameterName);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsImplicitRouteParam_IsFalse_WhenRouteTemplateIsEmpty()
    {
        // POST api/products with no route params — nothing to match
        var routeTemplate = "";
        var parameterName = "dto";

        var result = ProjectAnalyzer.IsImplicitRouteParam(routeTemplate, parameterName);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsImplicitRouteParam_IsTrue_WhenNestedRouteHasMultiplePlaceholders()
    {
        // Nested resource route — e.g. api/products/{id}/reviews/{reviewId}
        var routeTemplate = "api/products/{id}/reviews/{reviewId}";

        ProjectAnalyzer.IsImplicitRouteParam(routeTemplate, "id").Should().BeTrue();
        ProjectAnalyzer.IsImplicitRouteParam(routeTemplate, "reviewId").Should().BeTrue();
    }

    [Fact]
    public void ReadDtoProperties_PicksUpValidationAttributes()
    {
        var source = @"
        using System.ComponentModel.DataAnnotations;

        public class SampleDto
        {
            [Required]
            [MaxLength(50)]
            public string Name { get; set; }

            [Range(1, 10)]
            public int Age { get; set; }
        }";

        var compilation = CreateCompilation(source);
        var syntaxTree = compilation.SyntaxTrees.First();
        var semanticModel = compilation.GetSemanticModel(syntaxTree);

        var classDeclaration = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "SampleDto");

        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
        classSymbol.Should().NotBeNull();

        var diagnostics = compilation.GetDiagnostics();
        if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            throw new Exception(string.Join("\n", diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        }

        var properties = ProjectAnalyzer.ReadDtoProperties(classSymbol!);

        var nameProp = properties.Should().ContainSingle(p => p.Name == "Name").Subject;
        nameProp.ValidationAttributes.Should().Contain(a => a.Name == "Required");
        nameProp.ValidationAttributes.Should().Contain(a => a.Name == "MaxLength" && a.Arguments.Contains("50"));

        var ageProp = properties.Should().ContainSingle(p => p.Name == "Age").Subject;
        ageProp.ValidationAttributes.Should().Contain(a => a.Name == "Range" && a.Arguments.Contains("1") && a.Arguments.Contains("10"));
    }
}