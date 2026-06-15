using System.Xml.Linq;

namespace TestWire.cli.Generation;

public static class TestProjectGenerator
{
    public static void Generate(string targetCsprojPath, string outputDir, GenerationContext context)
    {
        var resolvedDir = File.Exists(outputDir) || outputDir.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || outputDir.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(outputDir)!
            : outputDir;

        var projectName = Path.GetFileNameWithoutExtension(targetCsprojPath);
        var testCsprojPath = Path.Combine(resolvedDir, $"{projectName}.Tests.csproj");

        var relativePath = Path.GetRelativePath(resolvedDir, targetCsprojPath);

        // Align ASP.NET testing package with the detected framework
        var mvcTestingVersion = context.TargetFramework.Contains("9.0") ? "9.0.0"
                              : context.TargetFramework.Contains("8.0") ? "8.0.0"
                              : context.TargetFramework.Contains("7.0") ? "7.0.0"
                              : "8.0.0"; // safe default

        var testingPackages = context.Framework == TestFramework.NUnit
            ? """
<PackageReference Include="NUnit" Version="4.2.2" />
<PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
"""
            : """
<PackageReference Include="xunit" Version="2.9.2" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
""";

        var content = $"""
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>{context.TargetFramework}</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
{testingPackages}
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="{mvcTestingVersion}" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="{relativePath}" />
  </ItemGroup>

</Project>
""";

        Directory.CreateDirectory(resolvedDir);

        // Write the .csproj if it doesn't exist
        if (!File.Exists(testCsprojPath) || context.OverwriteExisting)
        {
            File.WriteAllText(testCsprojPath, content);
        }

        // Write auth scaffold files - these are infrastructure files every
        // generated test project needs to compile and run against [Authorize] endpoints
        var authHandlerPath = Path.Combine(resolvedDir, "TestAuthHandler.cs");
        var factoryPath = Path.Combine(resolvedDir, "CustomWebApplicationFactory.cs");

        // Write these if they are missing or if we want to ensure latest version
        if (!File.Exists(authHandlerPath) || context.OverwriteExisting)
            TestFileWriter.Write(authHandlerPath, AuthScaffoldGenerator.GenerateTestAuthHandler(context.ProjectNamespace));

        if (!File.Exists(factoryPath) || context.OverwriteExisting)
            TestFileWriter.Write(factoryPath, AuthScaffoldGenerator.GenerateCustomWebApplicationFactory(context.ProjectNamespace));
    }
}