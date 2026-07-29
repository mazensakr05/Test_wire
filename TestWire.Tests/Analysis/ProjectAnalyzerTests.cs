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
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location)
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
        // Arrange
        var source = $@"
        using System;
        public class {attributeName}Attribute : Attribute {{ }}

        public class TestController
        {{
            [{attributeName}]
            public void TestMethod() {{ }}
        }}";

        var methodSymbol = GetMethodSymbol(source, "TestMethod");

        // Act
        var verb = ProjectAnalyzer.GetHttpVerb(methodSymbol);

        // Assert
        verb.Should().Be(expectedVerb);
    }

    [Fact]
    public void GetHttpVerb_ReturnsNullWhenNoVerbAttribute()
    {
        // Arrange
        var source = @"
        public class TestController
        {
            public void TestMethod() { }
        }";

        var methodSymbol = GetMethodSymbol(source, "TestMethod");

        // Act
        var verb = ProjectAnalyzer.GetHttpVerb(methodSymbol);

        // Assert
        verb.Should().BeNull();
    }

    [Fact]
    public void HasAttribute_ReturnsTrueWhenAttributeIsPresent()
    {
        // Arrange
        var source = @"
        using System;
        public class AuthorizeAttribute : Attribute { }

        public class TestController
        {
            [Authorize]
            public void ProtectedMethod() { }
        }";

        var methodSymbol = GetMethodSymbol(source, "ProtectedMethod");

        // Act
        var hasAttr = ProjectAnalyzer.HasAttribute(methodSymbol, "Authorize");

        // Assert
        hasAttr.Should().BeTrue();
    }
}
