using FluentAssertions;
using TestWire.cli.Generation;
using Xunit;

namespace TestWire.Tests.Generation;

public class TestValuesTests
{
    [Theory]
    [InlineData("int", "1")]
    [InlineData("string", "\"test\"")]
    [InlineData("bool", "true")]
    [InlineData("guid", "Guid.NewGuid()")]
    [InlineData("unknown_type", "null")]
    public void AsExpression_ReturnsCorrectCSharpExpression(string type, string expected)
    {
        var result = TestValues.AsExpression(type);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("int", "1")]
    [InlineData("string", "test")]
    [InlineData("guid", "00000000-0000-0000-0000-000000000001")]
    [InlineData("unknown_type", "1")]
    public void AsRouteSegment_ReturnsCorrectLiteral(string type, string expected)
    {
        var result = TestValues.AsRouteSegment(type);
        result.Should().Be(expected);
    }

    [Fact]
    public void AsNotFoundSegment_ReturnsFakeValues()
    {
        TestValues.AsNotFoundSegment("string").Should().Be("nonexistent-xyz-404");
        TestValues.AsNotFoundSegment("int").Should().Be("99999");
        TestValues.AsNotFoundSegment("guid").Should().NotBe("00000000-0000-0000-0000-000000000001");
    }

    [Theory]
    [InlineData(200, "HttpStatusCode.OK")]
    [InlineData(201, "HttpStatusCode.Created")]
    [InlineData(404, "HttpStatusCode.NotFound")]
    [InlineData(418, "(HttpStatusCode)418")]
    public void ToStatusCodeExpression_MapsCorrectly(int statusCode, string expected)
    {
        TestValues.ToStatusCodeExpression(statusCode).Should().Be(expected);
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, true)]
    [InlineData(204, false)] // No Content
    [InlineData(400, false)] // Bad Request
    [InlineData(500, false)] // Server Error
    public void HasResponseBody_ReturnsCorrectBoolean(int statusCode, bool expected)
    {
        TestValues.HasResponseBody(statusCode).Should().Be(expected);
    }

}
