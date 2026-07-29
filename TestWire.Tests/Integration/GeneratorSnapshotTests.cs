using FluentAssertions;
using TestWire.cli.Analysis;
using TestWire.cli.Generation;
using VerifyXunit;
using Xunit;
using System.IO;
using static VerifyXunit.Verifier;

namespace TestWire.Tests.Integration;

public class GeneratorSnapshotTests
{
    [Fact]
    public async Task ProductsController_SnapshotTest()
    {
        // Arrange
        // The tests run in bin/Debug/net9.0, so we need to step back to find SampleApi.csproj
        // Directory structure: TestWire/TestWire.Tests/bin/Debug/net9.0
        var csprojPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "SampleApi", "SampleApi.csproj"));
        
        File.Exists(csprojPath).Should().BeTrue($"SampleApi.csproj should exist at {csprojPath}");

        // Act - Run Roslyn analysis on the SampleApi project
        var controllers = await ProjectAnalyzer.AnalyzeAsync(csprojPath);

        // Find the ProductsController
        var productsController = controllers.FirstOrDefault(c => c.ClassName == "ProductsController");
        productsController.Should().NotBeNull();

        // Generate the test file string
        var context = new GenerationContext("SampleApi", "net8.0", TestFramework.XUnit);
        var generatedCode = TestFileGenerator.Generate(productsController!, context);

        // Assert - Snapshot the output
        await Verify(generatedCode);
    }
    
    [Fact]
    public async Task CategoriesController_SnapshotTest()
    {
        // Arrange
        var csprojPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "SampleApi", "SampleApi.csproj"));
        
        // Act
        var controllers = await ProjectAnalyzer.AnalyzeAsync(csprojPath);
        var categoriesController = controllers.FirstOrDefault(c => c.ClassName == "CategoriesController");
        categoriesController.Should().NotBeNull();

        // Generate the test file string
        var context = new GenerationContext("SampleApi", "net8.0", TestFramework.XUnit);
        var generatedCode = TestFileGenerator.Generate(categoriesController!, context);

        // Assert
        await Verify(generatedCode);
    }
}
