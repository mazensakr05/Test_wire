using System.Diagnostics;

namespace TestWire.cli.Verification;

public static class BuildVerifier
{
    public static (bool Success, string Output) Verify(string testProjectDirectory)
    {
        var psi = new ProcessStartInfo("dotnet", "build")
        {
            WorkingDirectory = testProjectDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            return (false, "Could not start dotnet build process.");

        string stdOut = process.StandardOutput.ReadToEnd();
        string stdErr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        var combinedOutput = stdOut + Environment.NewLine + stdErr;
        return (process.ExitCode == 0, combinedOutput);
    }
}