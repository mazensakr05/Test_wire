using System.Diagnostics;
using System.Text;

namespace TestWire.cli.Verification;

public static class BuildVerifier
{
    public static (bool Success, string Output) Verify(string testProjectDir, int timeoutMs = 60000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build",
            WorkingDirectory = testProjectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        var stdOutBuilder = new StringBuilder();
        var stdErrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdOutBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stdErrBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        bool exited = process.WaitForExit(timeoutMs);
        if (!exited)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return (false, $"Build verification timed out after {timeoutMs}ms and was killed.");
        }

        var combinedOutput = stdOutBuilder.ToString() + Environment.NewLine + stdErrBuilder.ToString();
        return (process.ExitCode == 0, combinedOutput);
    }
}