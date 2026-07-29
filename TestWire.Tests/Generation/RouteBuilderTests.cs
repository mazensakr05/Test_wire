using FluentAssertions;
using TestWire.cli.Analysis;
using TestWire.cli.Generation;
using Xunit;

namespace TestWire.Tests.Generation;

public class RouteBuilderTests
{
    [Fact]
    public void Build_ReplacesControllerToken_AndAppendsSegment()
    {
        // Arrange
        var baseRoute = "api/[controller]";
        var className = "ProductsController";
        var segment = "{id}";
        var parameters = new List<ParameterDetail>
        {
            new("id", "int", "System.Int32", false, true, false, false, new())
        };

        // Act
        var result = RouteBuilder.Build(baseRoute, className, segment, parameters);

        // Assert
        result.Should().Be("api/products/1");
    }

    [Fact]
    public void Build_AppendsQueryParametersCorrectly()
    {
        // Arrange
        var baseRoute = "api/[controller]";
        var className = "SearchController";
        var segment = "";
        var parameters = new List<ParameterDetail>
        {
            new("query", "string", "System.String", false, false, true, false, new()),
            new("page", "int", "System.Int32", false, false, true, false, new())
        };

        // Act
        var result = RouteBuilder.Build(baseRoute, className, segment, parameters);

        // Assert
        result.Should().Be("api/search?query=test&page=1");
    }

    [Fact]
    public void BuildNotFound_UsesNotFoundValues()
    {
        // Arrange
        var baseRoute = "api/[controller]";
        var className = "ProductsController";
        var segment = "{id}";
        var parameters = new List<ParameterDetail>
        {
            new("id", "int", "System.Int32", false, true, false, false, new())
        };

        // Act
        var result = RouteBuilder.BuildNotFound(baseRoute, className, segment, parameters);

        // Assert
        result.Should().Be("api/products/99999");
    }
}
