using FluentAssertions;
using System.Diagnostics;
using TestWire.cli.Verification;
using Xunit;

namespace TestWire.Tests.Verification;

public class BuildVerifierTests
{
    private static string GetFixturePath(string fixtureName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);
    }

    [Fact]
    public void Verify_ValidProject_ReturnsSuccess()
    {
        // Arrange
        var projectDir = GetFixturePath("ValidProject");

        // Act
        var result = TestWire.cli.Verification.BuildVerifier.Verify(projectDir);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().NotContain("error CS");
    }

    [Fact]
    public void Verify_BrokenProject_ReturnsFailureWithCompilerError()
    {
        // Arrange
        var projectDir = GetFixturePath("BrokenProject");

        // Act
        var result = TestWire.cli.Verification.BuildVerifier.Verify(projectDir);

        // Assert
        result.Success.Should().BeFalse();
        result.Output.Should().Contain("error CS");
    }

    [Fact]
    public void Verify_Timeout_ReturnsFailureAndDoesNotHang()
    {
        // Arrange
        var projectDir = GetFixturePath("ValidProject");
        var sw = Stopwatch.StartNew();

        // Act
        // Use an unreasonably short timeout (e.g. 1ms) so the build process is killed immediately
        var result = TestWire.cli.Verification.BuildVerifier.Verify(projectDir, timeoutMs: 1);
        sw.Stop();

        // Assert
        result.Success.Should().BeFalse();
        result.Output.Should().Contain("timed out after 1ms and was killed");
        sw.ElapsedMilliseconds.Should().BeLessThan(2000, "The test should not hang and wait for the full build to finish.");
    }
    
    // Note: The --no-verify option behavior is implemented at the command level 
    // in GenerateCommand.cs. BuildVerifier.Verify itself has no knowledge of it.
    // Testing that GenerateCommand respects --no-verify belongs in a GenerateCommand test.
}
